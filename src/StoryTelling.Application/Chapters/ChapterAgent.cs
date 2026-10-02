using System.Text;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Settings;

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
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);
        var assembled = _assembler.AssembleWriter(context);

        var gathered = await ChapterToolLoop
            .GatherAsync(_llmClient, connection, settings, assembled.Messages, PromptTemplates.WriterGather(), context.Snapshot, progress, cancellationToken)
            .ConfigureAwait(false);

        var messages = gathered.Messages;
        var toolCalls = gathered.ToolCalls;

        progress?.Report(new GenerationProgress("Writing", toolCalls));

        var write = new LlmRequest
        {
            Model = settings.Model,
            Messages = [.. messages, LlmMessage.User(PromptTemplates.WriterWrite(context.Chapter))],
            Temperature = settings.Temperature,
            MaxTokens = settings.MaxTokens,
        };

        var text = await StreamTextAsync(connection, write, cancellationToken).ConfigureAwait(false);
        if (GeneratedText.LooksTruncated(text))
        {
            progress?.Report(new GenerationProgress("Completing", toolCalls));
            var retry = write with
            {
                Messages =
                [
                    .. write.Messages,
                    LlmMessage.User("Your previous reply was cut off. Write the full chapter again (roughly 1500-2500 words) and end with a complete sentence."),
                ],
            };

            var completed = await StreamTextAsync(connection, retry, cancellationToken).ConfigureAwait(false);
            if (completed.Length >= text.Length)
            {
                text = completed;
            }
        }

        text = ChapterTextCleaner.StripLeadingTitle(text, context.Chapter);
        return new ChapterDraft(text, toolCalls);
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
}
