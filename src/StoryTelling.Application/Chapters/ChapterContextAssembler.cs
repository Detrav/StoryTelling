using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;

namespace StoryTelling.Application.Chapters;

public sealed class ChapterContextAssembler : IContextAssembler
{
    public const int DefaultTokenBudget = 4000;

    public const int DefaultRequiredSectionMaxChars = 6000;

    private const int CharsPerToken = 4;

    public ChapterContext AssembleWriter(WriterContext context)
    {
        var system = PromptTemplates.WriterSystem();
        var storyContext = BuildStoryContext(context);
        var task = "TASK\n\n" + PromptTemplates.WriterBrief(context.Chapter);
        var messages = new List<LlmMessage>
        {
            LlmMessage.System(system),
            LlmMessage.User(storyContext),
            LlmMessage.User(task),
        };
        var estimatedTokens = (system.Length + storyContext.Length + task.Length) / CharsPerToken;
        return new ChapterContext(messages, estimatedTokens);
    }

    public string BuildStoryContext(WriterContext context)
    {
        var budget = Math.Max(200, context.TokenBudget) * CharsPerToken;
        var sections = new List<string>();
        var used = 0;

        void AddRequired(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            sections.Add(text);
            used += text.Length;
        }

        void AddOptional(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var remaining = budget - used;
            if (remaining <= 0)
            {
                return;
            }

            if (text.Length > remaining)
            {
                text = text[..remaining] + "…[truncated]";
            }

            sections.Add(text);
            used += text.Length;
        }

        AddRequired(PromptTemplates.WriterWorldStyle(context.Snapshot.World));
        AddRequired(PromptTemplates.WriterPosition(context.Snapshot, context.Chapter));
        AddRequired(Truncate(PromptTemplates.WriterState(context.StateBefore), context.RequiredSectionMaxChars));
        AddRequired(PromptTemplates.WriterRecap(context.Snapshot, context.Chapter, context.RecentLoglineCount));
        AddRequired(PromptTemplates.OpenThreads(context.Snapshot));
        AddRequired(PromptTemplates.WriterCast(context.Snapshot));
        AddOptional(PromptTemplates.WriterWorldLore(context.Snapshot.World));
        AddOptional(PromptTemplates.WriterManifest(context.Snapshot, context.Chapter));

        return "STORY CONTEXT\n\n" + string.Join("\n\n", sections);
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…[truncated]";
}
