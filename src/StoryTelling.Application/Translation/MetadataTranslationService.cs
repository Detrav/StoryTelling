using System.Text.Json;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Settings;

namespace StoryTelling.Application.Translation;

public sealed class MetadataTranslationService : IMetadataTranslator
{
    private readonly ILlmClient _llmClient;
    private readonly ISettingsService _settingsService;

    public MetadataTranslationService(ILlmClient llmClient, ISettingsService settingsService)
    {
        _llmClient = llmClient;
        _settingsService = settingsService;
    }

    public async Task<MetadataTranslationResult> TranslateAsync(
        MetadataTranslationRequest request,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new GenerationProgress("Translating metadata", 0));

        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);

        var llmRequest = new LlmRequest
        {
            Model = settings.Model,
            Messages = PromptTemplates.BuildMetadataTranslation(
                request.LanguageCode,
                request.BookName,
                request.Annotation,
                request.ChapterTitles),
            Temperature = settings.TemperatureFor(LlmTask.Translation),
            MaxTokens = settings.MaxTokens,
        };

        var content = await _llmClient
            .CompleteJsonAsync(connection, llmRequest, "MetadataTranslation", MetadataTranslationSchema.Build(), cancellationToken)
            .ConfigureAwait(false);

        return Parse(content);
    }

    private static MetadataTranslationResult Parse(string content)
    {
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(content);
            root = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new LlmException(LlmErrorKind.InvalidResponse, $"The model returned invalid metadata JSON: {exception.Message}");
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new LlmException(LlmErrorKind.InvalidResponse, "The model returned metadata JSON that is not an object.");
        }

        var name = GetString(root, "name");
        var annotation = GetString(root, "annotation");

        var titles = new Dictionary<int, string>();
        if (root.TryGetProperty("chapterTitles", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in array.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object
                    || !element.TryGetProperty("number", out var numberElement)
                    || numberElement.ValueKind != JsonValueKind.Number
                    || !numberElement.TryGetInt32(out var number)
                    || number <= 0)
                {
                    continue;
                }

                var title = GetString(element, "title");
                if (!string.IsNullOrWhiteSpace(title))
                {
                    titles[number] = title.Trim();
                }
            }
        }

        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(annotation) && titles.Count == 0)
        {
            throw new LlmException(LlmErrorKind.InvalidResponse, "The model returned no translated metadata.");
        }

        return new MetadataTranslationResult(name.Trim(), annotation.Trim(), titles);
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}