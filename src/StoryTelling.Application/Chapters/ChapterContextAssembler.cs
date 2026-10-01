using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;

namespace StoryTelling.Application.Chapters;

public sealed class ChapterContextAssembler : IContextAssembler
{
    public const int DefaultTokenBudget = 2000;

    private const int CharsPerToken = 4;

    public ChapterContext AssembleWriter(WriterContext context)
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

        AddRequired(PromptTemplates.WriterBrief(context.Chapter));
        AddRequired(PromptTemplates.WriterFrame(context.Snapshot.Frame));
        AddRequired(PromptTemplates.WriterState(context.StateBefore));
        AddRequired(PromptTemplates.WriterPremise(context.Snapshot));
        AddOptional(PromptTemplates.WriterLore(context.Snapshot.Lore));
        AddOptional(PromptTemplates.WriterManifest(context.Snapshot, context.Chapter));

        var system = PromptTemplates.WriterSystem();
        var user = string.Join("\n\n", sections);
        var messages = new List<LlmMessage> { LlmMessage.System(system), LlmMessage.User(user) };
        var estimatedTokens = (system.Length + user.Length) / CharsPerToken;
        return new ChapterContext(messages, estimatedTokens);
    }
}
