using System.Text;
using System.Text.Json;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Knowledge;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Review;
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

    public async Task<ChapterEdit> EditAsync(
        Project project,
        Chapter chapter,
        string draft,
        WorldState stateBefore,
        EditorStage stage,
        IReadOnlyList<EditorIssue>? knownIssues = null,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(draft))
        {
            return new ChapterEdit(draft, [], EditorVerdict.Ok);
        }

        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);
        var knowledge = KnowledgeComposer.Compose(project, chapter.Number);
        var task = stage == EditorStage.Integrity ? LlmTask.EditorIntegrity : LlmTask.EditorCosmetic;

        var revised = stage == EditorStage.Integrity
            ? await RunIntegrityAsync(connection, settings, project, chapter, draft, stateBefore, knowledge, progress, cancellationToken).ConfigureAwait(false)
            : await RunCosmeticAsync(connection, settings, project, chapter, draft, stateBefore, progress, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(revised))
        {
            revised = draft;
        }

        revised = ChapterTextCleaner.StripLeadingTitle(revised, chapter);

        var styleNotes = await RepairStyleAsync(connection, settings, chapter, revised, project.World.Tense, cancellationToken).ConfigureAwait(false);
        if (styleNotes.Revised is { } corrected)
        {
            revised = corrected;
        }

        var notes = await ExtractNotesAsync(connection, settings, draft, revised, cancellationToken).ConfigureAwait(false);
        var allNotes = new List<EditorNote>(styleNotes.Notes);
        allNotes.AddRange(notes);

        var verdict = stage == EditorStage.Integrity
            ? await ValidateAsync(connection, settings, project, chapter, revised, stateBefore, knowledge, knownIssues, progress, cancellationToken).ConfigureAwait(false)
            : EditorVerdict.Ok;

        return new ChapterEdit(revised, allNotes, verdict);
    }

    private async Task<string> RunIntegrityAsync(
        LlmConnection connection,
        AppSettings settings,
        Project project,
        Chapter chapter,
        string draft,
        WorldState stateBefore,
        IReadOnlyList<KnowledgeEntry> knowledge,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var seed = EditorCanonPrompt.BuildIntegritySeed(project, chapter, stateBefore, knowledge);
        var gathered = await ChapterToolLoop
            .GatherAsync(_llmClient, connection, settings, seed, EditorCanonPrompt.BuildIntegrityTodo(project, chapter, stateBefore, knowledge), project, progress, cancellationToken, chapter.Number)
            .ConfigureAwait(false);

        progress?.Report(new GenerationProgress("Editing (integrity)", gathered.ToolCalls));
        return await ReviseAsync(connection, settings, gathered.Messages, LlmTask.EditorIntegrity, draft, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> RunCosmeticAsync(
        LlmConnection connection,
        AppSettings settings,
        Project project,
        Chapter chapter,
        string draft,
        WorldState stateBefore,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var seed = EditorCanonPrompt.BuildCosmeticSeed(project, chapter, stateBefore, settings.RecentLoglineCount);
        var gathered = await ChapterToolLoop
            .GatherAsync(_llmClient, connection, settings, seed, PromptTemplates.EditorGather(), project, progress, cancellationToken, chapter.Number)
            .ConfigureAwait(false);

        progress?.Report(new GenerationProgress("Editing (cosmetic)", gathered.ToolCalls));
        return await ReviseAsync(connection, settings, gathered.Messages, LlmTask.EditorCosmetic, draft, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> ReviseAsync(
        LlmConnection connection,
        AppSettings settings,
        IReadOnlyList<LlmMessage> messages,
        LlmTask task,
        string draft,
        CancellationToken cancellationToken)
    {
        var write = new LlmRequest
        {
            Model = settings.Model,
            Messages = [.. messages, .. PromptTemplates.BuildEditorWrite(draft)],
            Temperature = settings.TemperatureFor(task),
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

        return revised;
    }

    private async Task<EditorVerdict> ValidateAsync(
        LlmConnection connection,
        AppSettings settings,
        Project project,
        Chapter chapter,
        string revised,
        WorldState stateBefore,
        IReadOnlyList<KnowledgeEntry> knowledge,
        IReadOnlyList<EditorIssue>? knownIssues,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new GenerationProgress("Checking integrity", 0));
        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = EditorVerdictPrompt.Build(project, chapter, revised, stateBefore, knowledge, knownIssues),
            Temperature = settings.TemperatureFor(LlmTask.EditorIntegrity),
            MaxTokens = settings.MaxTokens,
        };

        try
        {
            var content = await _llmClient
                .CompleteJsonAsync(connection, request, "EditorVerdict", EditorVerdictSchema.Build(), cancellationToken)
                .ConfigureAwait(false);
            return ParseVerdict(content);
        }
        catch (LlmException)
        {
            return EditorVerdict.Ok;
        }
        catch (JsonException)
        {
            return EditorVerdict.Ok;
        }
    }

    private static EditorVerdict ParseVerdict(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(GeneratedText.StripCodeFence(content.Trim()));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("issues", out var items)
                || items.ValueKind != JsonValueKind.Array)
            {
                return EditorVerdict.Ok;
            }

            var issues = new List<EditorIssue>();
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

                issues.Add(new EditorIssue(severity, detail, GetString(element, "reference").Trim()));
            }

            return new EditorVerdict(!issues.Any(issue => issue.Severity == ReviewSeverity.Error), issues);
        }
        catch (JsonException)
        {
            return EditorVerdict.Ok;
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
