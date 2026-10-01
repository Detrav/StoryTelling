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
    public const int MaxStructuredAttempts = 3;

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

        for (var attempt = 1; ; attempt++)
        {
            var content = await _llmClient
                .CompleteJsonAsync(connection, finalRequest, GenerationTargets.SchemaName(request.Target), schema, cancellationToken)
                .ConfigureAwait(false);

            var parsed = ParseOptions(content, request.Target);
            var options = FilterDistinct(parsed, request.Avoid);
            if (options.Count > 0 || attempt >= MaxStructuredAttempts)
            {
                return options;
            }

            progress?.Report(new GenerationProgress($"Retrying ({attempt})", session?.ToolCalls ?? 0));
            var reminder = parsed.Count > 0
                ? "Every option reused a name that is already taken or duplicated another option. Reply again with fresh options that use new, distinct names and none from the forbidden list."
                : "That reply was not valid JSON matching the schema. Reply again with ONLY the JSON and nothing else.";
            finalRequest = finalRequest with
            {
                Messages =
                [
                    .. finalRequest.Messages,
                    LlmMessage.Assistant(content),
                    LlmMessage.User(reminder),
                ],
            };
        }
    }

    private static IReadOnlyList<GenerationOption> FilterDistinct(IReadOnlyList<GenerationOption> options, IReadOnlyList<string> avoid)
    {
        if (options.Count == 0)
        {
            return options;
        }

        var forbidden = new HashSet<string>(
            avoid.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()),
            StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<GenerationOption>();

        foreach (var option in options)
        {
            if (OptionKey(option.Fields) is not { } key)
            {
                result.Add(option);
                continue;
            }

            if (forbidden.Contains(key) || !seen.Add(key))
            {
                continue;
            }

            result.Add(option);
        }

        return result;
    }

    private static string? OptionKey(IReadOnlyDictionary<string, string> fields)
    {
        if (fields.TryGetValue("Title", out var title) && !string.IsNullOrWhiteSpace(title))
        {
            return title.Trim();
        }

        return fields.TryGetValue("Name", out var name) && !string.IsNullOrWhiteSpace(name) ? name.Trim() : null;
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
        var text = GeneratedText.StripCodeFence(content.Trim());
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
        var empty = new Dictionary<string, string>();

        if (element.ValueKind == JsonValueKind.String && specs.Count == 1)
        {
            var single = ReadField(element, out var singleImplausible);
            return !singleImplausible && single is not null
                ? new Dictionary<string, string> { [specs[0].Field] = single }
                : empty;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return empty;
        }

        var fields = new Dictionary<string, string>();
        foreach (var spec in specs)
        {
            if (!element.TryGetProperty(spec.JsonName, out var value))
            {
                continue;
            }

            var text = ReadField(value, out var implausible);
            if (implausible)
            {
                return empty;
            }

            if (text is not null)
            {
                fields[spec.Field] = text;
            }
        }

        return fields;
    }

    private static string? ReadField(JsonElement value, out bool implausible)
    {
        implausible = false;

        if (value.ValueKind == JsonValueKind.Array)
        {
            var items = new List<string>();
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var text = GeneratedText.Clean(item.GetString());
                if (text is null)
                {
                    continue;
                }

                if (!GeneratedText.IsPlausible(text))
                {
                    implausible = true;
                    return null;
                }

                items.Add(text);
            }

            return items.Count == 0 ? null : string.Join(", ", items);
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var scalar = GeneratedText.Clean(value.GetString());
        if (scalar is null)
        {
            return null;
        }

        if (!GeneratedText.IsPlausible(scalar))
        {
            implausible = true;
            return null;
        }

        return scalar;
    }
}
