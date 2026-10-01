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

        var seed = PromptTemplates.BuildReview(snapshot, brief);
        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = seed,
            Temperature = settings.Temperature,
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

    private static IReadOnlyList<ReviewFinding> ParseFindings(string content)
    {
        var findings = new List<ReviewFinding>();

        try
        {
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("findings", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return findings;
            }

            foreach (var element in items.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var title = GetString(element, "title");
                var detail = GetString(element, "detail");
                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(detail))
                {
                    continue;
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
        }
        catch (JsonException)
        {
        }

        return findings;
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
