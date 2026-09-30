using System.Text.Json;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Settings;

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
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);
        var llmRequest = new LlmRequest
        {
            Model = settings.Model,
            Messages = PromptTemplates.Build(request),
            Temperature = settings.Temperature,
            MaxTokens = settings.MaxTokens,
        };

        var schema = GenerationTargets.BuildSchema(request.Target, request.Variants);
        var content = await _llmClient
            .CompleteJsonAsync(connection, llmRequest, GenerationTargets.SchemaName(request.Target), schema, cancellationToken)
            .ConfigureAwait(false);

        return ParseOptions(content, request.Target);
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
