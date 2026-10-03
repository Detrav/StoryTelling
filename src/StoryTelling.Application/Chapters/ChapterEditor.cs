using System.Text;
using System.Text.Json;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public sealed class ChapterEditor : IChapterEditor
{
    private readonly ILlmClient _llmClient;
    private readonly ISettingsService _settingsService;
    private readonly IContextAssembler _assembler;
    private readonly ITextDiff _textDiff;

    public ChapterEditor(ILlmClient llmClient, ISettingsService settingsService, IContextAssembler assembler, ITextDiff textDiff)
    {
        _llmClient = llmClient;
        _settingsService = settingsService;
        _assembler = assembler;
        _textDiff = textDiff;
    }

    public async Task<EditorChecklistVerdict> CheckAsync(
        WriterContext context,
        string text,
        IReadOnlyList<EditorCheck> checks,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text) || checks.Count == 0)
        {
            return EditorChecklistVerdict.Empty;
        }

        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);
        var seed = EditorCanonPrompt.BuildCheckerMessages(_assembler, context, text, checks);

        var gathered = await ChapterToolLoop
            .GatherAsync(_llmClient, connection, settings, seed, EditorCanonPrompt.CheckerGather(), context.Snapshot, progress, cancellationToken, context.Chapter.Number)
            .ConfigureAwait(false);

        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = gathered.Messages,
            Temperature = settings.TemperatureFor(LlmTask.EditorChecker),
            ReasoningEffort = settings.ReasoningEffortFor(LlmTask.EditorChecker),
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
        WriterContext context,
        string text,
        EditorCheck check,
        string reason,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);
        var seed = EditorCanonPrompt.BuildFixerMessages(_assembler, context, check, reason);

        var gathered = await ChapterToolLoop
            .GatherAsync(_llmClient, connection, settings, seed, EditorCanonPrompt.FixerGather(), context.Snapshot, progress, cancellationToken, context.Chapter.Number)
            .ConfigureAwait(false);

        progress?.Report(new GenerationProgress($"Fixing {check.Label}", gathered.ToolCalls));

        var write = new LlmRequest
        {
            Model = settings.Model,
            Messages = [.. gathered.Messages, .. PromptTemplates.BuildEditorWrite(text)],
            Temperature = settings.TemperatureFor(LlmTask.EditorFixer),
            ReasoningEffort = settings.ReasoningEffortFor(LlmTask.EditorFixer),
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
                    LlmMessage.User("Your previous reply was cut off. Return the complete corrected chapter and end with a full sentence."),
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
            return text;
        }

        return ChapterTextCleaner.StripLeadingTitle(revised, context.Chapter);
    }

    public async Task<ChapterEdit> CosmeticAsync(
        WriterContext context,
        string text,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ChapterEdit(text, []);
        }

        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);
        var seed = EditorCanonPrompt.BuildCosmeticMessages(_assembler, context);

        var gathered = await ChapterToolLoop
            .GatherAsync(_llmClient, connection, settings, seed, PromptTemplates.EditorGather(), context.Snapshot, progress, cancellationToken, context.Chapter.Number)
            .ConfigureAwait(false);

        progress?.Report(new GenerationProgress("Polishing", gathered.ToolCalls));

        var write = new LlmRequest
        {
            Model = settings.Model,
            Messages = [.. gathered.Messages, .. PromptTemplates.BuildEditorWrite(text)],
            Temperature = settings.TemperatureFor(LlmTask.EditorCosmetic),
            ReasoningEffort = settings.ReasoningEffortFor(LlmTask.EditorCosmetic),
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

        revised = ChapterTextCleaner.StripLeadingTitle(revised, context.Chapter);

        var styleNotes = await RepairStyleAsync(connection, settings, context.Chapter, revised, context.Snapshot.World.Tense, cancellationToken).ConfigureAwait(false);
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
        Domain.Chapter chapter,
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
            ReasoningEffort = settings.ReasoningEffortFor(LlmTask.StyleRepair),
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
            ReasoningEffort = settings.ReasoningEffortFor(LlmTask.StyleRepair),
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
