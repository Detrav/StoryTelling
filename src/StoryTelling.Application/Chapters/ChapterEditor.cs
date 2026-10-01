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
    private const double DeterministicTemperature = 0.3;

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
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(draft))
        {
            return new ChapterEdit(draft, []);
        }

        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);

        var seed = PromptTemplates.BuildEditorSeed(chapter, stateBefore, project);
        var gathered = await ChapterToolLoop
            .GatherAsync(_llmClient, connection, settings, seed, PromptTemplates.EditorGather(), project, progress, cancellationToken)
            .ConfigureAwait(false);

        progress?.Report(new GenerationProgress("Editing", gathered.ToolCalls));

        var write = new LlmRequest
        {
            Model = settings.Model,
            Messages = [.. gathered.Messages, .. PromptTemplates.BuildEditorWrite(draft)],
            Temperature = settings.Temperature,
            MaxTokens = settings.MaxTokens,
        };

        var revised = await StreamTextAsync(connection, write, cancellationToken).ConfigureAwait(false);
        if (GeneratedText.LooksTruncated(revised))
        {
            progress?.Report(new GenerationProgress("Completing", gathered.ToolCalls));
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
            revised = draft;
        }

        revised = ChapterTextCleaner.StripLeadingTitle(revised, chapter);

        var notes = await ExtractNotesAsync(connection, settings, draft, revised, cancellationToken).ConfigureAwait(false);
        return new ChapterEdit(revised, notes);
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
            Temperature = Math.Min(settings.Temperature, DeterministicTemperature),
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
