using System.Text;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Llm;
using StoryTelling.Domain;

namespace StoryTelling.Application.Prompts;

public static class EditorCanonPrompt
{
    public static string CheckerSystem() =>
        "Role: You are a strict continuity checker. You do NOT rewrite anything.\n"
        + "Objective: For each checklist item, decide only whether the chapter is correct (ok) or violates it (not ok), and give a one-line reason.\n"
        + "The world, the narrative frame (point of view, tense), the initial state and the knowledge base are inviolable facts. Judge the chapter against them, never the other way around.\n"
        + "Constraints: Work in English only. Do not propose fixes, do not quote long passages. Base every verdict strictly on the material given.\n"
        + "Output: Only a JSON object that matches the required schema: one entry per checklist id.";

    public static IReadOnlyList<LlmMessage> BuildCheckerSeed(
        Project project,
        Chapter chapter,
        WorldState stateBefore,
        IReadOnlyList<KnowledgeEntry> knowledge,
        IReadOnlyList<EditorCheck> checks)
    {
        var user = new StringBuilder();
        user.AppendLine(BuildCanon(project, chapter, stateBefore, knowledge, includeBrief: true));

        user.AppendLine();
        user.AppendLine("Checklist — answer every item by id (ok = true, violation = false):");
        foreach (var check in checks)
        {
            user.AppendLine($"- {check.Id}: {check.Question}");
        }

        user.AppendLine();
        user.AppendLine("Chapter prose:");
        const int proseBudget = 16000;
        var prose = chapter.ContentOriginal?.Trim() ?? string.Empty;
        user.AppendLine(prose.Length <= proseBudget ? prose : prose[..proseBudget] + "…[truncated]");

        user.AppendLine();
        user.AppendLine("Return one object per checklist id with { id, ok, reason }. Use ok = false only for a real violation.");

        return [LlmMessage.System(CheckerSystem()), LlmMessage.User(user.ToString())];
    }

    public static IReadOnlyList<LlmMessage> BuildFixerSeed(
        Project project,
        Chapter chapter,
        WorldState stateBefore,
        IReadOnlyList<KnowledgeEntry> knowledge,
        EditorCheck check,
        string draft,
        string reason)
    {
        var system = "Role: You are a meticulous continuity fixer.\n"
            + $"Objective: Fix the single \"{check.Label}\" problem described below and change nothing else.\n"
            + "The world, the narrative frame, the initial state and the knowledge base are inviolable. Correct the prose to match the facts, never the other way around.\n"
            + "Constraints: Change ONLY what the described problem requires. Keep every other sentence, story beat, character detail, name and fact exactly as it is — do not rephrase, shorten, expand, reorder or 'improve' anything that is not part of the problem. Preserve the author's voice, the point of view, the tense and the full length. Write in English only. Never mention chapter numbers or the book itself. Return a COMPLETE chapter, not a fragment.\n"
            + "Output: Only the full revised chapter prose — no title, headings or commentary.";

        var user = new StringBuilder();
        user.AppendLine(BuildCanon(project, chapter, stateBefore, knowledge, includeBrief: true));
        user.AppendLine();
        user.AppendLine($"Problem to fix ({check.Id}): {reason}");
        user.AppendLine(check.FixInstruction);
        user.AppendLine();
        user.AppendLine("Apply the smallest possible change that removes this problem. Leave every other word of the chapter untouched.");
        user.AppendLine();
        user.AppendLine("Chapter to fix:");
        user.AppendLine(draft);
        user.AppendLine();
        user.AppendLine("Return the full corrected chapter text.");

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }

    public static string CosmeticSystem() =>
        "Role: You are a meticulous fiction editor.\n"
        + "Objective: Polish the chapter draft for prose quality, pacing, repetition, clarity and style.\n"
        + "Constraints: Preserve the author's voice, the point of view, the tense, the established facts and "
        + "every story beat. Keep the draft's full length and detail — never summarize or shorten it. Write in "
        + "English only. Never mention chapter numbers or the book itself.\n"
        + "Output: Only the revised chapter prose — no title, headings or commentary.\n"
        + "Do not revise until you are asked to.";

    public static IReadOnlyList<LlmMessage> BuildCosmeticSeed(Project project, Chapter chapter, WorldState stateBefore, int recentLoglineCount)
    {
        return [LlmMessage.System(CosmeticSystem()), LlmMessage.User(BuildCosmeticTodo(project, chapter, stateBefore, recentLoglineCount))];
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

    private static string BuildCanon(Project project, Chapter chapter, WorldState stateBefore, IReadOnlyList<KnowledgeEntry> knowledge, bool includeBrief)
    {
        var user = new StringBuilder();
        if (includeBrief)
        {
            user.AppendLine(PromptTemplates.WriterBrief(chapter));
        }

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
}
