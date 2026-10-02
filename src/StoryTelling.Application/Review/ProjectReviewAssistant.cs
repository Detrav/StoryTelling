using System.Text.Json;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Settings;
using StoryTelling.Application.Story;
using StoryTelling.Application.Tools;
using StoryTelling.Domain;

namespace StoryTelling.Application.Review;

public sealed class ProjectReviewAssistant : IProjectReviewAssistant
{
    private static readonly ReviewFocus[] _passes = [ReviewFocus.Numbers, ReviewFocus.Facts];

    private readonly ILlmClient _llmClient;
    private readonly ISettingsService _settingsService;

    public ProjectReviewAssistant(ILlmClient llmClient, ISettingsService settingsService)
    {
        _llmClient = llmClient;
        _settingsService = settingsService;
    }

    public async Task<IReadOnlyList<ReviewFinding>> ReviewAsync(
        Project snapshot,
        string brief,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);

        var findings = new List<ReviewFinding>();
        foreach (var focus in _passes)
        {
            findings.AddRange(await RunPassAsync(snapshot, brief, settings, connection, focus, progress, cancellationToken).ConfigureAwait(false));
        }

        return Normalize(findings, snapshot);
    }

    private async Task<IReadOnlyList<ReviewFinding>> RunPassAsync(
        Project snapshot,
        string brief,
        AppSettings settings,
        LlmConnection connection,
        ReviewFocus focus,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var seed = PromptTemplates.BuildReview(snapshot, brief, focus);
        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = seed,
            Temperature = settings.TemperatureFor(LlmTask.Review),
            MaxTokens = settings.MaxTokens,
        };

        var messages = seed;
        if (settings.MaxToolCalls > 0)
        {
            var toolset = new StoryToolset(new StoryQuery(snapshot));
            var tools = toolset.Definitions
                .Select(definition => new LlmTool(definition.Name, definition.Description, definition.Parameters))
                .ToList();

            var agent = new ToolAgent(_llmClient);
            var outcome = await agent
                .GatherAsync(connection, request, tools, toolset.Invoke, settings.MaxToolCalls, progress: progress, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            messages = outcome.Messages;
        }

        progress?.Report(new GenerationProgress("Reviewing", 0));

        var content = await _llmClient
            .CompleteJsonAsync(connection, request with { Messages = messages }, "ReviewFindings", ReviewSchema.Build(), cancellationToken)
            .ConfigureAwait(false);

        return ParseFindings(content);
    }

    private static IReadOnlyList<ReviewFinding> Normalize(IReadOnlyList<ReviewFinding> findings, Project snapshot)
    {
        var result = new List<ReviewFinding>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in findings)
        {
            var normalized = finding with { Fix = NormalizeFix(finding.Fix, snapshot) };
            var key = $"{normalized.Area}|{normalized.Reference?.Trim()}|{normalized.Title.Trim()}";
            if (seen.Add(key))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    private static ReviewFix? NormalizeFix(ReviewFix? fix, Project snapshot)
    {
        if (fix is null || fix.IsEmpty)
        {
            return fix;
        }

        var edits = fix.Edits.Where(edit => !IsNoOp(edit, snapshot)).ToList();
        return edits.Count == 0 ? null : new ReviewFix(edits);
    }

    private static bool IsNoOp(ReviewEdit edit, Project snapshot)
    {
        if (edit.Target != GenerationTarget.Knowledge)
        {
            return false;
        }

        var entry = snapshot.Knowledge.FirstOrDefault(candidate =>
            string.Equals(candidate.Title, edit.Reference?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return false;
        }

        var current = CurrentValue(entry, edit.Field);
        return current is not null
            && string.Equals(Collapse(current), Collapse(edit.Value), StringComparison.Ordinal);
    }

    private static string? CurrentValue(KnowledgeEntry entry, string field) => field.Trim().ToLowerInvariant() switch
    {
        "title" => entry.Title,
        "kind" => entry.Kind.ToString(),
        "content" => entry.Content,
        "tags" => string.Join(", ", entry.Tags),
        _ => null,
    };

    private static string Collapse(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static IReadOnlyList<ReviewFinding> ParseFindings(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("findings", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var findings = new List<ReviewFinding>();
            foreach (var element in items.EnumerateArray())
            {
                AddFinding(element, findings);
            }

            return findings;
        }
        catch (JsonException)
        {
            return SalvageFindings(content);
        }
    }

    private static IReadOnlyList<ReviewFinding> SalvageFindings(string content)
    {
        var arrayStart = IndexOfFindingsArray(content);
        if (arrayStart < 0)
        {
            return [];
        }

        var findings = new List<ReviewFinding>();
        var depth = 0;
        var start = -1;
        var inString = false;
        var escaped = false;

        for (var index = arrayStart; index < content.Length; index++)
        {
            var character = content[index];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    if (depth == 0)
                    {
                        start = index;
                    }

                    depth++;
                    break;
                case '}':
                    if (depth > 0)
                    {
                        depth--;
                        if (depth == 0 && start >= 0)
                        {
                            TryAddSalvaged(content[start..(index + 1)], findings);
                            start = -1;
                        }
                    }

                    break;
                case ']' when depth == 0:
                    return findings;
            }
        }

        return findings;
    }

    private static int IndexOfFindingsArray(string content)
    {
        var marker = content.IndexOf("\"findings\"", StringComparison.Ordinal);
        return marker < 0 ? -1 : content.IndexOf('[', marker);
    }

    private static void TryAddSalvaged(string json, List<ReviewFinding> findings)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                AddFinding(document.RootElement, findings);
            }
        }
        catch (JsonException)
        {
        }
    }

    private static void AddFinding(JsonElement element, List<ReviewFinding> findings)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var title = GetString(element, "title");
        var detail = GetString(element, "detail");
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(detail))
        {
            return;
        }

        findings.Add(new ReviewFinding(
            ParseSeverity(GetString(element, "severity")),
            ParseArea(GetString(element, "area")),
            title.Trim(),
            detail.Trim(),
            EmptyToNull(GetString(element, "suggestion")),
            ParseFix(element),
            EmptyToNull(GetString(element, "reference"))));
    }

    private static ReviewFix? ParseFix(JsonElement element)
    {
        if (!element.TryGetProperty("fix", out var fix) || fix.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!fix.TryGetProperty("edits", out var edits) || edits.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var result = new List<ReviewEdit>();
        foreach (var edit in edits.EnumerateArray())
        {
            if (edit.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!Enum.TryParse<GenerationTarget>(GetString(edit, "target"), ignoreCase: true, out var target))
            {
                continue;
            }

            var field = GetString(edit, "field");
            if (!GenerationTargets.HasField(target, field))
            {
                continue;
            }

            result.Add(new ReviewEdit(target, GetString(edit, "reference").Trim(), field, GetString(edit, "value")));
        }

        return new ReviewFix(result);
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ReviewSeverity ParseSeverity(string value) =>
        Enum.TryParse<ReviewSeverity>(value, ignoreCase: true, out var severity) ? severity : ReviewSeverity.Info;

    private static ReviewArea ParseArea(string value) =>
        Enum.TryParse<ReviewArea>(value, ignoreCase: true, out var area) ? area : ReviewArea.General;
}
