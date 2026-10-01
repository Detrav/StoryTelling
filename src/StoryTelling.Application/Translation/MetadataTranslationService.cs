using System.Text.Json;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Settings;

namespace StoryTelling.Application.Translation;

public sealed class MetadataTranslationService : IMetadataTranslator
{
    private const double DeterministicTemperature = 0.2;

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
            Temperature = Math.Min(settings.Temperature, DeterministicTemperature),
            MaxTokens = settings.MaxTokens,
        };

        var content = await _llmClient
            .CompleteJsonAsync(connection, llmRequest, "MetadataTranslation", MetadataTranslationSchema.Build(), cancellationToken)
            .ConfigureAwait(false);

        return Parse(content, request);
    }

    private static MetadataTranslationResult Parse(string content, MetadataTranslationRequest request)
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

        var name = GetString(root, "name");
        var annotation = GetString(root, "annotation");
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(annotation))
        {
            throw new LlmException(LlmErrorKind.InvalidResponse, "The model returned no translated metadata.");
        }

        var titles = new Dictionary<int, string>();
        if (root.TryGetProperty("chapterTitles", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in array.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object
                    || !element.TryGetProperty("number", out var numberElement)
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

        return new MetadataTranslationResult(
            string.IsNullOrWhiteSpace(name) ? request.BookName : name.Trim(),
            string.IsNullOrWhiteSpace(annotation) ? request.Annotation : annotation.Trim(),
            titles);
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}