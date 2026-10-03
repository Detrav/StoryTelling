using System.Text.Json;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.Application.Review;

public sealed class ProjectReviewAssistant : IProjectReviewAssistant
{
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
        ReviewCheck check,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);

        progress?.Report(new GenerationProgress("Reviewing", 0));

        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = PromptTemplates.BuildReview(snapshot, brief, check),
            Temperature = settings.TemperatureFor(LlmTask.Review),
            ReasoningEffort = settings.ReasoningEffortFor(LlmTask.Review),
            MaxTokens = settings.MaxTokens,
        };

        var content = await CompleteWithRetryAsync(connection, request, check.Reconcile, cancellationToken).ConfigureAwait(false);

        return ReviewDeduplicator.Deduplicate(ParseFindings(content), snapshot);
    }

    private async Task<string> CompleteWithRetryAsync(LlmConnection connection, LlmRequest request, bool includeReconciliation, CancellationToken cancellationToken)
    {
        const int attempts = 2;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await _llmClient
                    .CompleteJsonAsync(connection, request, "ReviewFindings", ReviewSchema.Build(includeReconciliation), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (LlmException) when (attempt < attempts)
            {
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            }
        }
    }

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

            if (!Enum.TryParse<ReviewEditOperation>(GetString(edit, "op"), ignoreCase: true, out var operation))
            {
                operation = ReviewEditOperation.Set;
            }

            if (!Enum.TryParse<GenerationTarget>(GetString(edit, "target"), ignoreCase: true, out var target))
            {
                target = GenerationTarget.Knowledge;
            }

            var reference = GetString(edit, "reference").Trim();
            var value = GetString(edit, "value");
            var field = GetString(edit, "field");

            switch (operation)
            {
                case ReviewEditOperation.Set:
                    if (!GenerationTargets.HasField(target, field))
                    {
                        continue;
                    }

                    break;
                case ReviewEditOperation.AddTag:
                case ReviewEditOperation.RemoveTag:
                    if (value.Trim().Length == 0)
                    {
                        continue;
                    }

                    field = "Tags";
                    break;
                case ReviewEditOperation.Create:
                    if (reference.Length == 0)
                    {
                        continue;
                    }

                    break;
                case ReviewEditOperation.Delete:
                    if (reference.Length == 0)
                    {
                        continue;
                    }

                    break;
            }

            var kind = Enum.TryParse<KnowledgeKind>(GetString(edit, "kind"), ignoreCase: true, out var parsedKind)
                ? parsedKind
                : (KnowledgeKind?)null;
            var tags = ReadTags(edit);

            result.Add(new ReviewEdit(target, reference, field, value, operation, kind, tags));
        }

        return new ReviewFix(result);
    }

    private static IReadOnlyList<string>? ReadTags(JsonElement edit)
    {
        if (!edit.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var values = tags.EnumerateArray()
            .Where(tag => tag.ValueKind == JsonValueKind.String)
            .Select(tag => tag.GetString()?.Trim())
            .Where(tag => !string.IsNullOrEmpty(tag))
            .Select(tag => tag!)
            .ToList();

        return values.Count == 0 ? null : values;
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ReviewSeverity ParseSeverity(string value) =>
        Enum.TryParse<ReviewSeverity>(value, ignoreCase: true, out var severity) ? severity : ReviewSeverity.Info;

    private static ReviewArea ParseArea(string value) =>
        Enum.TryParse<ReviewArea>(value, ignoreCase: true, out var area) ? area : ReviewArea.General;
}
