using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Settings;
using StoryTelling.Application.Story;
using StoryTelling.Application.Tools;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

internal static class ChapterToolLoop
{
    public static async Task<ToolAgentOutcome> GatherAsync(
        ILlmClient llmClient,
        LlmConnection connection,
        AppSettings settings,
        IReadOnlyList<LlmMessage> seed,
        string gatherInstruction,
        Project snapshot,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken,
        int? beforeNumber = null)
    {
        if (settings.MaxToolCalls <= 0)
        {
            return new ToolAgentOutcome(seed, 0);
        }

        var toolset = new StoryToolset(new StoryQuery(snapshot, beforeNumber));
        var tools = toolset.Definitions
            .Select(definition => new LlmTool(definition.Name, definition.Description, definition.Parameters))
            .ToList();

        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = [.. seed, LlmMessage.User(gatherInstruction)],
            Temperature = settings.TemperatureFor(LlmTask.Writer),
            ReasoningEffort = settings.ReasoningEffortFor(LlmTask.Writer),
            MaxTokens = Math.Min(settings.MaxTokens, 1024),
        };

        return await new ToolAgent(llmClient)
            .GatherAsync(connection, request, tools, toolset.Invoke, settings.MaxToolCalls, settings.ToolResultMaxChars, progress, cancellationToken)
            .ConfigureAwait(false);
    }
}
