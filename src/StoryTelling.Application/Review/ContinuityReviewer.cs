using System.Text.Json;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Knowledge;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.Application.Review;

public sealed class ContinuityReviewer : IContinuityReviewer
{
    private readonly ILlmClient _llmClient;
    private readonly ISettingsService _settingsService;

    public ContinuityReviewer(ILlmClient llmClient, ISettingsService settingsService)
    {
        _llmClient = llmClient;
        _settingsService = settingsService;
    }

    public async Task<IReadOnlyList<ContinuityFinding>> ReviewAsync(
        Project project,
        Chapter chapter,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);
        var knowledge = KnowledgeComposer.Compose(project, chapter.Number);

        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = PromptTemplates.BuildContinuityReview(project, chapter, knowledge),
            Temperature = settings.TemperatureFor(LlmTask.Continuity),
            ReasoningEffort = settings.ReasoningEffortFor(LlmTask.Continuity),
            MaxTokens = settings.MaxTokens,
        };

        try
        {
            var content = await _llmClient
                .CompleteJsonAsync(connection, request, "ContinuityReport", ContinuityReportSchema.Build(), cancellationToken)
                .ConfigureAwait(false);
            return Parse(content);
        }
        catch (LlmException)
        {
            return [];
        }
    }

    private static List<ContinuityFinding> Parse(string content)
    {
        var findings = new List<ContinuityFinding>();
        try
        {
            using var document = JsonDocument.Parse(GeneratedText.StripCodeFence(content.Trim()));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("findings", out var items)
                || items.ValueKind != JsonValueKind.Array)
            {
                return findings;
            }

            foreach (var element in items.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object
                    || !Enum.TryParse<ReviewSeverity>(Get(element, "severity"), ignoreCase: true, out var severity))
                {
                    continue;
                }

                var title = Get(element, "title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                findings.Add(new ContinuityFinding(severity, title.Trim(), Get(element, "detail").Trim(), Get(element, "reference").Trim()));
            }
        }
        catch (JsonException)
        {
        }

        return findings;
    }

    private static string Get(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
