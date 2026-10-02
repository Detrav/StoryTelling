using System.Text;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Llm;
using StoryTelling.Domain;

namespace StoryTelling.Application.Prompts;

public static class EditorCanonPrompt
{
    public static string IntegritySystem() =>
        "Role: You are a meticulous continuity editor.\n"
        + "Objective: Revise the chapter without breaking canon, and report any canon violation you cannot fix.\n"
        + "The world, the narrative frame (point of view, tense), the initial state and the knowledge base are inviolable facts. If the draft contradicts them — a character who is alive in the facts acting as dead, an age or relationship that conflicts with an entry, a fixed world rule broken, a fact stated differently from an entry — fix the prose to match the facts, never the other way around.\n"
        + "Constraints: Preserve the author's voice and the chapter's full length and detail. Write in English only. Never mention chapter numbers or the book itself.\n"
        + "Output: Only the revised chapter prose — no title, headings or commentary.\n"
        + "Do not revise until you are asked to.";

    public static IReadOnlyList<LlmMessage> BuildIntegritySeed(Project project, Chapter chapter, WorldState stateBefore, IReadOnlyList<KnowledgeEntry> knowledge)
    {
        return [LlmMessage.System(IntegritySystem()), LlmMessage.User(BuildIntegrityTodo(project, chapter, stateBefore, knowledge))];
    }

    public static string BuildIntegrityTodo(Project project, Chapter chapter, WorldState stateBefore, IReadOnlyList<KnowledgeEntry> knowledge)
    {
        var user = new StringBuilder();
        user.AppendLine(PromptTemplates.WriterBrief(chapter));

        var frame = PromptTemplates.WriterWorldStyle(project.World);
        if (frame.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(frame);
        }

        user.AppendLine();
        user.AppendLine(PromptTemplates.WriterPosition(project, chapter));

        var state = PromptTemplates.WriterState(stateBefore);
        if (state.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(state);
        }

        var world = PromptTemplates.WriterWorldLore(project.World);
        if (world.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(world);
            user.AppendLine("The world above is inviolable: never contradict it.");
        }

        var cast = PromptTemplates.WriterCast(project);
        if (cast.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(cast);
            user.AppendLine("The cast above is inviolable: a character who is alive stays alive, and ages, relations and facts must match.");
        }

        var threads = PromptTemplates.OpenThreads(project);
        if (threads.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(threads);
        }

        return user.ToString();
    }

    public static IReadOnlyList<LlmMessage> BuildCosmeticSeed(Project project, Chapter chapter, WorldState stateBefore, int recentLoglineCount)
    {
        return [LlmMessage.System(EditorSystem()), LlmMessage.User(BuildCosmeticTodo(project, chapter, stateBefore, recentLoglineCount))];
    }

    public static string BuildCosmeticTodo(Project project, Chapter chapter, WorldState stateBefore, int recentLoglineCount)
    {
        var user = new StringBuilder();
        user.AppendLine(PromptTemplates.WriterBrief(chapter));

        var frame = PromptTemplates.WriterWorldStyle(project.World);
        if (frame.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(frame);
        }

        user.AppendLine();
        user.AppendLine(PromptTemplates.WriterPosition(project, chapter));

        var state = PromptTemplates.WriterState(stateBefore);
        if (state.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(state);
        }

        var story = PromptTemplates.WriterRecap(project, chapter, recentLoglineCount);
        if (story.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(story);
        }

        var threads = PromptTemplates.OpenThreads(project);
        if (threads.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(threads);
        }

        return user.ToString();
    }

    private static string EditorSystem() =>
        "Role: You are a meticulous fiction editor.\n"
        + "Objective: Polish the chapter draft for prose quality, pacing, repetition, clarity and style.\n"
        + "Constraints: Preserve the author's voice, the point of view, the tense, the established facts and "
        + "every story beat. Keep the draft's full length and detail — never summarize or shorten it. Write in "
        + "English only. Never mention chapter numbers or the book itself.\n"
        + "Output: Only the revised chapter prose — no title, headings or commentary.\n"
        + "Do not revise until you are asked to.";
}
