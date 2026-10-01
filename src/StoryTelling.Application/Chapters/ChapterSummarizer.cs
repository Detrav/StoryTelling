using System.Text.Json;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public sealed class ChapterSummarizer : IChapterSummarizer
{
    public const int MaxStructuredAttempts = 3;

    private const double DeterministicTemperature = 0.3;

    private readonly ILlmClient _llmClient;
    private readonly ISettingsService _settingsService;

    public ChapterSummarizer(ILlmClient llmClient, ISettingsService settingsService)
    {
        _llmClient = llmClient;
        _settingsService = settingsService;
    }

    public async Task<ChapterSummary> SummarizeAsync(
        Chapter chapter,
        WorldState stateBefore,
        IReadOnlyList<KnowledgeEntry> knowledge,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);
        var schema = ChapterSummarySchema.Build();

        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = PromptTemplates.BuildSummarizer(chapter, stateBefore, knowledge),
            Temperature = Math.Min(settings.Temperature, DeterministicTemperature),
            MaxTokens = settings.MaxTokens,
        };

        for (var attempt = 1; ; attempt++)
        {
            progress?.Report(new GenerationProgress("Summarizing", 0));

            var content = await _llmClient
                .CompleteJsonAsync(connection, request, "ChapterSummary", schema, cancellationToken)
                .ConfigureAwait(false);

            if (TryParse(content, out var summary))
            {
                return summary;
            }

            if (attempt >= MaxStructuredAttempts)
            {
                throw new LlmException(LlmErrorKind.InvalidResponse, "The model did not return a valid chapter summary.");
            }

            request = request with
            {
                Messages =
                [
                    .. request.Messages,
                    LlmMessage.Assistant(content),
                    LlmMessage.User("That reply was not valid JSON matching the schema. Reply again with ONLY the JSON and nothing else."),
                ],
            };
        }
    }

    private static bool TryParse(string content, out ChapterSummary summary)
    {
        summary = null!;

        try
        {
            using var document = JsonDocument.Parse(GeneratedText.StripCodeFence(content.Trim()));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var logline = ReadString(root, "logline");
            var timeAndPlace = ReadString(root, "timeAndPlace");
            if (logline is null || timeAndPlace is null)
            {
                return false;
            }

            var description = ReadString(root, "description") ?? string.Empty;
            var changes = ParseChanges(root);
            summary = new ChapterSummary(logline, new WorldState { TimeAndPlace = timeAndPlace, Description = description }, changes);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<KnowledgeChange> ParseChanges(JsonElement root)
    {
        var changes = new List<KnowledgeChange>();
        if (!root.TryGetProperty("knowledgeChanges", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return changes;
        }

        foreach (var element in items.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object
                || !Enum.TryParse<KnowledgeChangeOperation>(GetString(element, "operation"), ignoreCase: true, out var operation))
            {
                continue;
            }

            var title = GeneratedText.Clean(GetString(element, "title"));
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            changes.Add(new KnowledgeChange
            {
                Operation = operation,
                Kind = ParseKind(GetString(element, "kind")),
                Title = StripKindPrefix(title),
                Tags = ParseTags(element),
                Content = GetString(element, "content"),
                Reason = GetString(element, "reason"),
            });
        }

        return changes;
    }

    private static string StripKindPrefix(string title)
    {
        var trimmed = title.Trim();
        if (trimmed.StartsWith('[') && trimmed.IndexOf(']') is var close && close > 0 && close < trimmed.Length - 1)
        {
            return trimmed[(close + 1)..].Trim();
        }

        return trimmed;
    }

    private static string? ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = GeneratedText.Clean(value.GetString());
        return text is null || !GeneratedText.IsPlausible(text) ? null : text;
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static List<string> ParseTags(JsonElement element)
    {
        var tags = new List<string>();
        if (!element.TryGetProperty("tags", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return tags;
        }

        foreach (var tag in value.EnumerateArray())
        {
            if (tag.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(tag.GetString()))
            {
                tags.Add(tag.GetString()!.Trim());
            }
        }

        return tags;
    }

    private static KnowledgeKind ParseKind(string value) =>
        Enum.TryParse<KnowledgeKind>(value, ignoreCase: true, out var kind) ? kind : KnowledgeKind.Note;
}
