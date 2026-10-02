using System.Text.Json;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Review;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public sealed class ChapterSummarizer : IChapterSummarizer
{
    public const int MaxStructuredAttempts = 3;

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

        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = PromptTemplates.BuildChapterSummary(chapter, stateBefore, knowledge),
            Temperature = settings.TemperatureFor(LlmTask.Summarizer),
            MaxTokens = settings.MaxTokens,
        };

        for (var attempt = 1; ; attempt++)
        {
            progress?.Report(new GenerationProgress("Summarizing", 0));

            var content = await _llmClient
                .CompleteJsonAsync(connection, request, "ChapterSummary", ChapterSummarySchema.Build(), cancellationToken)
                .ConfigureAwait(false);

            if (TryParse(content, out var summary))
            {
                if (!HasMeta(summary))
                {
                    return summary;
                }

                if (attempt >= MaxStructuredAttempts)
                {
                    return Sanitize(summary);
                }
            }
            else if (attempt >= MaxStructuredAttempts)
            {
                throw new LlmException(LlmErrorKind.InvalidResponse, "The model did not return a valid chapter summary.");
            }

            request = request with
            {
                Messages =
                [
                    .. request.Messages,
                    LlmMessage.Assistant(content),
                    LlmMessage.User("That reply was not valid JSON matching the schema, a required field was empty, or it referenced chapter numbers. Reply again with ONLY the JSON and nothing else; never mention chapter numbers or the story-so-far anywhere."),
                ],
            };
        }
    }

    private static bool HasMeta(ChapterSummary summary) =>
        StyleGuard.HasMeta(summary.Logline)
        || StyleGuard.HasMeta(summary.WorldState.Situation)
        || summary.KnowledgeChanges.Any(change => StyleGuard.HasMeta(change.Title) || StyleGuard.HasMeta(change.Content));

    private static ChapterSummary Sanitize(ChapterSummary summary) => summary with
    {
        Logline = StyleGuard.RemoveMeta(summary.Logline),
        WorldState = new WorldState
        {
            TimeAndPlace = summary.WorldState.TimeAndPlace,
            Situation = StyleGuard.RemoveMeta(summary.WorldState.Situation),
        },
        KnowledgeChanges =
        [
            .. summary.KnowledgeChanges.Select(change => new KnowledgeChange
            {
                Operation = change.Operation,
                EntryId = change.EntryId,
                Status = change.Status,
                Kind = change.Kind,
                Title = StyleGuard.RemoveMeta(change.Title),
                Tags = [.. change.Tags],
                Content = StyleGuard.RemoveMeta(change.Content),
                Reason = change.Reason,
            }),
        ],
    };

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
            var situation = ReadString(root, "situation");
            if (logline is null || timeAndPlace is null || situation is null)
            {
                return false;
            }

            summary = new ChapterSummary(
                logline,
                new WorldState { TimeAndPlace = timeAndPlace, Situation = situation },
                ParseChanges(root),
                ParseContinuity(root),
                ParseRewrites(root));
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

            var kind = ParseKind(GetString(element, "kind"));
            var entryId = ParseEntryId(GetString(element, "entryId"));
            if (operation == KnowledgeChangeOperation.Create && entryId is null)
            {
                entryId = Guid.NewGuid();
            }

            changes.Add(new KnowledgeChange
            {
                Operation = operation,
                EntryId = entryId,
                Status = kind == KnowledgeKind.Thread ? ParseStatus(GetString(element, "status")) : null,
                Kind = kind,
                Title = CleanTitle(title),
                Tags = ParseTags(element),
                Content = GetString(element, "content"),
                Reason = GetString(element, "reason"),
            });
        }

        return changes;
    }

    private static List<ContinuityIssue> ParseContinuity(JsonElement root)
    {
        var issues = new List<ContinuityIssue>();
        if (!root.TryGetProperty("continuityNotes", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return issues;
        }

        foreach (var element in items.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object
                || !Enum.TryParse<ReviewSeverity>(GetString(element, "severity"), ignoreCase: true, out var severity))
            {
                continue;
            }

            var detail = GetString(element, "detail").Trim();
            if (detail.Length == 0)
            {
                continue;
            }

            issues.Add(new ContinuityIssue(severity, detail, GetString(element, "reference").Trim()));
        }

        return issues;
    }

    private static List<DirectionRewrite> ParseRewrites(JsonElement root)
    {
        var rewrites = new List<DirectionRewrite>();
        if (!root.TryGetProperty("directionRewrites", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return rewrites;
        }

        foreach (var element in items.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty("chapterNumber", out var number) || number.ValueKind != JsonValueKind.Number
                || !number.TryGetInt32(out var chapterNumber)
                || !element.TryGetProperty("direction", out var direction) || direction.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var text = direction.GetString()?.Trim() ?? string.Empty;
            if (text.Length > 0)
            {
                rewrites.Add(new DirectionRewrite(chapterNumber, text));
            }
        }

        return rewrites;
    }

    private static Guid? ParseEntryId(string value) =>
        Guid.TryParse(value?.Trim(), out var id) ? id : null;

    private static KnowledgeStatus? ParseStatus(string value) =>
        Enum.TryParse<KnowledgeStatus>(value, ignoreCase: true, out var status) ? status : null;

    private static string CleanTitle(string title)
    {
        var trimmed = title.Trim();

        if (trimmed.StartsWith('[') && trimmed.IndexOf(']') is var close && close > 0 && close < trimmed.Length - 1)
        {
            trimmed = trimmed[(close + 1)..].Trim();
        }

        if (trimmed.EndsWith(']') && trimmed.LastIndexOf('[') is var open && open > 0)
        {
            trimmed = trimmed[..open].Trim();
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
