using System.Text;
using System.Text.Json;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Knowledge;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public sealed class ChapterEditor : IChapterEditor
{
    private readonly ILlmClient _llmClient;
    private readonly ISettingsService _settingsService;
    private readonly ITextDiff _textDiff;

    public ChapterEditor(ILlmClient llmClient, ISettingsService settingsService, ITextDiff textDiff)
    {
        _llmClient = llmClient;
        _settingsService = settingsService;
        _textDiff = textDiff;
    }

    public async Task<EditorChecklistVerdict> CheckAsync(
        Project project,
        Chapter chapter,
        string text,
        WorldState stateBefore,
        IReadOnlyList<EditorCheck> checks,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text) || checks.Count == 0)
        {
            return EditorChecklistVerdict.Empty;
        }

        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);
        var knowledge = KnowledgeComposer.Compose(project, chapter.Number);
        var inspecting = new Chapter { Number = chapter.Number, Title = chapter.Title, ContentOriginal = text };

        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = EditorCanonPrompt.BuildCheckerSeed(project, inspecting, stateBefore, knowledge, checks),
            Temperature = settings.TemperatureFor(LlmTask.EditorChecker),
            MaxTokens = settings.MaxTokens,
        };

        try
        {
            var content = await _llmClient
                .CompleteJsonAsync(connection, request, "EditorChecklist", EditorChecklistSchema.Build(), cancellationToken)
                .ConfigureAwait(false);
            return ParseVerdict(content, checks);
        }
        catch (LlmException)
        {
            return EditorChecklistVerdict.Empty;
        }
        catch (JsonException)
        {
            return EditorChecklistVerdict.Empty;
        }
    }

    public async Task<string> FixAsync(
        Project project,
        Chapter chapter,
        string text,
        WorldState stateBefore,
        EditorCheck check,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);
        var knowledge = KnowledgeComposer.Compose(project, chapter.Number);
        var inspecting = new Chapter { Number = chapter.Number, Title = chapter.Title, ContentOriginal = text };

        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = EditorCanonPrompt.BuildFixerSeed(project, inspecting, stateBefore, knowledge, check, text, reason),
            Temperature = settings.TemperatureFor(LlmTask.EditorFixer),
            MaxTokens = settings.MaxTokens,
        };

        var revised = await StreamTextAsync(connection, request, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(revised))
        {
            return text;
        }

        return ChapterTextCleaner.StripLeadingTitle(revised, chapter);
    }

    public async Task<ChapterEdit> CosmeticAsync(
        Project project,
        Chapter chapter,
        string text,
        WorldState stateBefore,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ChapterEdit(text, []);
        }

        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);
        var inspecting = new Chapter { Number = chapter.Number, Title = chapter.Title, ContentOriginal = text };

        var seed = EditorCanonPrompt.BuildCosmeticSeed(project, inspecting, stateBefore, settings.RecentLoglineCount);
        var gathered = await ChapterToolLoop
            .GatherAsync(_llmClient, connection, settings, seed, PromptTemplates.EditorGather(), project, null, cancellationToken, chapter.Number)
            .ConfigureAwait(false);

        var write = new LlmRequest
        {
            Model = settings.Model,
            Messages = [.. gathered.Messages, .. PromptTemplates.BuildEditorWrite(text)],
            Temperature = settings.TemperatureFor(LlmTask.EditorCosmetic),
            MaxTokens = settings.MaxTokens,
        };

        var revised = await StreamTextAsync(connection, write, cancellationToken).ConfigureAwait(false);
        if (GeneratedText.LooksTruncated(revised))
        {
            var retry = write with
            {
                Messages =
                [
                    .. write.Messages,
                    LlmMessage.User("Your previous reply was cut off. Return the complete revised chapter and end with a full sentence."),
                ],
            };

            var completed = await StreamTextAsync(connection, retry, cancellationToken).ConfigureAwait(false);
            if (completed.Length >= revised.Length)
            {
                revised = completed;
            }
        }

        if (string.IsNullOrWhiteSpace(revised))
        {
            revised = text;
        }

        revised = ChapterTextCleaner.StripLeadingTitle(revised, chapter);

        var styleNotes = await RepairStyleAsync(connection, settings, chapter, revised, project.World.Tense, cancellationToken).ConfigureAwait(false);
        if (styleNotes.Revised is { } corrected)
        {
            revised = corrected;
        }

        var notes = await ExtractNotesAsync(connection, settings, text, revised, cancellationToken).ConfigureAwait(false);
        var allNotes = new List<EditorNote>(styleNotes.Notes);
        allNotes.AddRange(notes);
        return new ChapterEdit(revised, allNotes);
    }

    private static EditorChecklistVerdict ParseVerdict(string content, IReadOnlyList<EditorCheck> checks)
    {
        try
        {
            using var document = JsonDocument.Parse(GeneratedText.StripCodeFence(content.Trim()));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("checks", out var items)
                || items.ValueKind != JsonValueKind.Array)
            {
                return EditorChecklistVerdict.Empty;
            }

            var byId = new Dictionary<string, EditorCheckResult>(StringComparer.OrdinalIgnoreCase);
            foreach (var element in items.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var id = GetString(element, "id").Trim();
                if (id.Length == 0 || EditorChecks.Find(id) is null)
                {
                    continue;
                }

                var ok = element.TryGetProperty("ok", out var okValue) && okValue.ValueKind == JsonValueKind.True;
                byId[id] = new EditorCheckResult(id, ok, GetString(element, "reason").Trim());
            }

            var results = new List<EditorCheckResult>();
            foreach (var check in checks)
            {
                if (byId.TryGetValue(check.Id, out var result))
                {
                    results.Add(result);
                }
            }

            return new EditorChecklistVerdict(results);
        }
        catch (JsonException)
        {
            return EditorChecklistVerdict.Empty;
        }
    }

    private async Task<(string? Revised, IReadOnlyList<EditorNote> Notes)> RepairStyleAsync(
        LlmConnection connection,
        AppSettings settings,
        Chapter chapter,
        string revised,
        string declaredTense,
        CancellationToken cancellationToken)
    {
        var violations = StyleGuard.FindViolations(revised);
        var drifts = StyleGuard.DriftsFromTense(revised, declaredTense);
        if (violations.Count == 0 && !drifts)
        {
            return (null, []);
        }

        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = PromptTemplates.BuildEditorRepair(revised, violations, drifts, declaredTense),
            Temperature = settings.TemperatureFor(LlmTask.StyleRepair),
            MaxTokens = settings.MaxTokens,
        };

        var repaired = await StreamTextAsync(connection, request, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(repaired))
        {
            repaired = revised;
        }

        repaired = ChapterTextCleaner.StripLeadingTitle(repaired, chapter);

        var remaining = StyleGuard.FindViolations(repaired);
        var stillDrifts = StyleGuard.DriftsFromTense(repaired, declaredTense);
        var notes = new List<EditorNote>();
        if (remaining.Count > 0)
        {
            notes.Add(new EditorNote { Kind = EditorNoteKind.Other, Text = $"Style violation left in the prose: {string.Join(", ", remaining)}." });
        }

        if (stillDrifts)
        {
            notes.Add(new EditorNote { Kind = EditorNoteKind.Style, Text = $"Tense drift left in the prose (declared: {declaredTense})." });
        }

        return (repaired, notes);
    }

    private async Task<IReadOnlyList<EditorNote>> ExtractNotesAsync(
        LlmConnection connection,
        AppSettings settings,
        string original,
        string revised,
        CancellationToken cancellationToken)
    {
        var digest = BuildChangesDigest(original, revised);
        if (string.IsNullOrWhiteSpace(digest))
        {
            return [];
        }

        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = PromptTemplates.BuildEditorNotes(digest),
            Temperature = settings.TemperatureFor(LlmTask.StyleRepair),
            MaxTokens = settings.MaxTokens,
        };

        try
        {
            var content = await _llmClient
                .CompleteJsonAsync(connection, request, "EditorNotes", EditorNotesSchema.Build(), cancellationToken)
                .ConfigureAwait(false);
            return ParseNotes(content);
        }
        catch (LlmException)
        {
            return [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private string BuildChangesDigest(string original, string revised)
    {
        var patch = _textDiff.CreatePatch(original, revised);
        if (patch.IsEmpty)
        {
            return string.Empty;
        }

        const int limit = 8000;
        var builder = new StringBuilder();
        var used = 0;

        foreach (var hunk in patch.Hunks)
        {
            var before = string.Join('\n', hunk.OldLines).Trim();
            var after = string.Join('\n', hunk.NewLines).Trim();
            var block = $"Before:\n{before}\nAfter:\n{after}\n\n";
            if (used + block.Length > limit)
            {
                builder.Append("…[more changes truncated]\n");
                break;
            }

            builder.Append(block);
            used += block.Length;
        }

        return builder.ToString().Trim();
    }

    private async Task<string> StreamTextAsync(LlmConnection connection, LlmRequest request, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        await foreach (var delta in _llmClient.StreamAsync(connection, request, cancellationToken).ConfigureAwait(false))
        {
            builder.Append(delta);
        }

        return builder.ToString().Trim();
    }

    private static List<EditorNote> ParseNotes(string content)
    {
        var notes = new List<EditorNote>();

        try
        {
            using var document = JsonDocument.Parse(GeneratedText.StripCodeFence(content.Trim()));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("changes", out var changes)
                || changes.ValueKind != JsonValueKind.Array)
            {
                return notes;
            }

            foreach (var element in changes.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var text = GetString(element, "note");
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                notes.Add(new EditorNote
                {
                    Kind = ParseKind(GetString(element, "kind")),
                    Text = text.Trim(),
                });
            }
        }
        catch (JsonException)
        {
        }

        return notes;
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static EditorNoteKind ParseKind(string value) =>
        Enum.TryParse<EditorNoteKind>(value, ignoreCase: true, out var kind) ? kind : EditorNoteKind.Other;
}
