using System.Text.Json;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Settings;
using StoryTelling.Application.Story;
using StoryTelling.Application.Tools;

namespace StoryTelling.Application.Generation;

public sealed class GenerationAssistant : IGenerationAssistant
{
    private readonly ILlmClient _llmClient;
    private readonly ISettingsService _settingsService;

    public GenerationAssistant(ILlmClient llmClient, ISettingsService settingsService)
    {
        _llmClient = llmClient;
        _settingsService = settingsService;
    }

    public async Task<IReadOnlyList<GenerationOption>> GenerateAsync(
        GenerationRequest request,
        GenerationSession? session = null,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);

        var messages = await GatherAsync(connection, request, settings, session, progress, cancellationToken).ConfigureAwait(false);

        progress?.Report(new GenerationProgress("Generating", session?.ToolCalls ?? 0));

        var finalRequest = new LlmRequest
        {
            Model = settings.Model,
            Messages = messages,
            Temperature = settings.Temperature,
            MaxTokens = settings.MaxTokens,
        };

        var schema = GenerationTargets.BuildSchema(request.Target, request.Variants);
        var content = await _llmClient
            .CompleteJsonAsync(connection, finalRequest, GenerationTargets.SchemaName(request.Target), schema, cancellationToken)
            .ConfigureAwait(false);

        return ParseOptions(content, request.Target);
    }

    private async Task<IReadOnlyList<LlmMessage>> GatherAsync(
        LlmConnection connection,
        GenerationRequest request,
        AppSettings settings,
        GenerationSession? session,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (session is { Gathered: true } && string.Equals(session.Brief, request.Brief, StringComparison.Ordinal))
        {
            return session.Messages;
        }

        var snapshot = request.Snapshot;
        var useTools = snapshot is not null && settings.MaxToolCalls > 0;
        var seed = PromptTemplates.Build(request, useTools);

        if (!useTools || snapshot is null)
        {
            return seed;
        }

        var toolset = new StoryToolset(new StoryQuery(snapshot));
        var tools = toolset.Definitions
            .Select(definition => new LlmTool(definition.Name, definition.Description, definition.Parameters))
            .ToList();

        var agent = new ToolAgent(_llmClient);
        var request_ = new LlmRequest
        {
            Model = settings.Model,
            Messages = seed,
            Temperature = settings.Temperature,
            MaxTokens = settings.MaxTokens,
        };

        var outcome = await agent
            .GatherAsync(connection, request_, tools, toolset.Invoke, settings.MaxToolCalls, progress: progress, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (session is not null)
        {
            session.Brief = request.Brief;
            session.Messages = outcome.Messages;
            session.ToolCalls = outcome.ToolCalls;
            session.Gathered = true;
        }

        return outcome.Messages;
    }

    private static IReadOnlyList<GenerationOption> ParseOptions(string content, GenerationTarget target)
    {
        var specs = GenerationTargets.Fields(target);
        var text = StripCodeFence(content.Trim());
        var fromJson = TryParseJson(text, specs);
        if (fromJson.Count > 0)
        {
            return fromJson;
        }

        if (specs.Count == 1 && text.Length > 0)
        {
            return [new GenerationOption(new Dictionary<string, string> { [specs[0].Field] = text })];
        }

        return [];
    }

    private static List<GenerationOption> TryParseJson(string text, IReadOnlyList<GenerationFieldSpec> specs)
    {
        var result = new List<GenerationOption>();
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                Collect(root, specs, result);
            }
            else if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("options", out var options)
                && options.ValueKind == JsonValueKind.Array)
            {
                Collect(options, specs, result);
            }
        }
        catch (JsonException)
        {
        }

        return result;
    }

    private static void Collect(JsonElement array, IReadOnlyList<GenerationFieldSpec> specs, List<GenerationOption> result)
    {
        foreach (var element in array.EnumerateArray())
        {
            var fields = ReadOption(element, specs);
            if (fields.Count > 0)
            {
                result.Add(new GenerationOption(fields));
            }
        }
    }

    private static Dictionary<string, string> ReadOption(JsonElement element, IReadOnlyList<GenerationFieldSpec> specs)
    {
        var fields = new Dictionary<string, string>();

        if (element.ValueKind == JsonValueKind.String && specs.Count == 1)
        {
            var single = element.GetString()?.Trim();
            if (!string.IsNullOrEmpty(single))
            {
                fields[specs[0].Field] = single;
            }

            return fields;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return fields;
        }

        foreach (var spec in specs)
        {
            if (element.TryGetProperty(spec.JsonName, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString()?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    fields[spec.Field] = text;
                }
            }
        }

        return fields;
    }

    private static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstBreak = text.IndexOf('\n');
        if (firstBreak < 0)
        {
            return text;
        }

        var body = text[(firstBreak + 1)..];
        var closing = body.LastIndexOf("```", StringComparison.Ordinal);
        return (closing < 0 ? body : body[..closing]).Trim();
    }
}
