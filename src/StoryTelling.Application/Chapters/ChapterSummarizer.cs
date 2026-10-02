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
        string previousStorySoFar = "",
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);

        var briefing = await BriefAsync(connection, settings, chapter, stateBefore, knowledge, progress, cancellationToken).ConfigureAwait(false);
        var storySoFar = await SyncAsync(connection, settings, previousStorySoFar, briefing, chapter.Number, cancellationToken).ConfigureAwait(false);

        return new ChapterSummary(briefing.Logline, briefing.WorldState, briefing.KnowledgeChanges, storySoFar);
    }

    private async Task<ChapterBriefing> BriefAsync(
        LlmConnection connection,
        AppSettings settings,
        Chapter chapter,
        WorldState stateBefore,
        IReadOnlyList<KnowledgeEntry> knowledge,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = PromptTemplates.BuildChapterBriefing(chapter, stateBefore, knowledge),
            Temperature = Math.Min(settings.Temperature, DeterministicTemperature),
            MaxTokens = settings.MaxTokens,
        };

        for (var attempt = 1; ; attempt++)
        {
            progress?.Report(new GenerationProgress("Summarizing", 0));

            var content = await _llmClient
                .CompleteJsonAsync(connection, request, "ChapterBriefing", ChapterBriefingSchema.Build(), cancellationToken)
                .ConfigureAwait(false);

            if (TryParseBriefing(content, out var briefing))
            {
                return briefing;
            }

            if (attempt >= MaxStructuredAttempts)
            {
                throw new LlmException(LlmErrorKind.InvalidResponse, "The model did not return a valid chapter briefing.");
            }

            request = request with
            {
                Messages =
                [
                    .. request.Messages,
                    LlmMessage.Assistant(content),
                    LlmMessage.User("That reply was not valid JSON matching the schema, or a required field was empty. Reply again with ONLY the JSON and nothing else."),
                ],
            };
        }
    }

    private async Task<string> SyncAsync(
        LlmConnection connection,
        AppSettings settings,
        string previousStorySoFar,
        ChapterBriefing briefing,
        int chapterNumber,
        CancellationToken cancellationToken)
    {
        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = PromptTemplates.BuildChapterStorySync(previousStorySoFar, briefing, chapterNumber),
            Temperature = Math.Min(settings.Temperature, DeterministicTemperature),
            MaxTokens = settings.MaxTokens,
        };

        string? last = null;
        for (var attempt = 1; ; attempt++)
        {
            var content = await _llmClient
                .CompleteJsonAsync(connection, request, "ChapterStorySync", ChapterStorySyncSchema.Build(), cancellationToken)
                .ConfigureAwait(false);

            if (TryParseStory(content, out var storySoFar))
            {
                last = storySoFar;
                if (previousStorySoFar.Length == 0 || !Equivalent(storySoFar, previousStorySoFar))
                {
                    return storySoFar;
                }
            }

            if (attempt >= MaxStructuredAttempts)
            {
                break;
            }

            request = request with
            {
                Messages =
                [
                    .. request.Messages,
                    LlmMessage.Assistant(content),
                    LlmMessage.User("The story so far did not change. Fold this chapter in: it must add what happened here while keeping the opening and the arc. Reply with ONLY the JSON."),
                ],
            };
        }

        return last ?? previousStorySoFar;
    }

    private static bool TryParseBriefing(string content, out ChapterBriefing briefing)
    {
        briefing = null!;

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
            var description = ReadString(root, "description");
            if (logline is null || timeAndPlace is null || description is null)
            {
                return false;
            }

            briefing = new ChapterBriefing(
                logline,
                new WorldState { TimeAndPlace = timeAndPlace, Description = description },
                ParseChanges(root));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseStory(string content, out string story)
    {
        story = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(GeneratedText.StripCodeFence(content.Trim()));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var text = RawString(root, "storySoFar")?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            story = text;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool Equivalent(string left, string right) =>
        Normalize(left).Equals(Normalize(right), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

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
        var text = GeneratedText.Clean(RawString(element, property));
        return text is null || !GeneratedText.IsPlausible(text) ? null : text;
    }

    private static string? RawString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

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
