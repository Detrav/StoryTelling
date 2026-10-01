using System.Text;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Settings;
using StoryTelling.Application.Story;
using StoryTelling.Application.Tools;

namespace StoryTelling.Application.Chapters;

public sealed class ChapterAgent : IChapterAgent
{
    private readonly ILlmClient _llmClient;
    private readonly ISettingsService _settingsService;
    private readonly IContextAssembler _assembler;

    public ChapterAgent(ILlmClient llmClient, ISettingsService settingsService, IContextAssembler assembler)
    {
        _llmClient = llmClient;
        _settingsService = settingsService;
        _assembler = assembler;
    }

    public async Task<ChapterDraft> WriteAsync(
        WriterContext context,
        IProgress<GenerationProgress>? progress = null,
        Func<string, Task>? onDelta = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);
        var assembled = _assembler.AssembleWriter(context);

        var messages = assembled.Messages;
        var toolCalls = 0;

        if (settings.MaxToolCalls > 0)
        {
            var toolset = new StoryToolset(new StoryQuery(context.Snapshot));
            var tools = toolset.Definitions
                .Select(definition => new LlmTool(definition.Name, definition.Description, definition.Parameters))
                .ToList();

            var gather = new LlmRequest
            {
                Model = settings.Model,
                Messages = [.. messages, LlmMessage.User(PromptTemplates.WriterGather())],
                Temperature = settings.Temperature,
                MaxTokens = settings.MaxTokens,
            };

            var outcome = await new ToolAgent(_llmClient)
                .GatherAsync(connection, gather, tools, toolset.Invoke, settings.MaxToolCalls, progress: progress, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            messages = outcome.Messages;
            toolCalls = outcome.ToolCalls;
        }

        progress?.Report(new GenerationProgress("Writing", toolCalls));

        var write = new LlmRequest
        {
            Model = settings.Model,
            Messages = [.. messages, LlmMessage.User(PromptTemplates.WriterWrite(context.Chapter))],
            Temperature = settings.Temperature,
            MaxTokens = settings.MaxTokens,
        };

        var builder = new StringBuilder();
        await foreach (var delta in _llmClient.StreamAsync(connection, write, cancellationToken).ConfigureAwait(false))
        {
            builder.Append(delta);
            if (onDelta is not null)
            {
                await onDelta(delta).ConfigureAwait(false);
            }
        }

        return new ChapterDraft(builder.ToString().Trim(), toolCalls);
    }
}
