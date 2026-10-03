using System.Text;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Llm;

namespace StoryTelling.Application.Prompts;

public static class EditorCanonPrompt
{
    public static string CheckerSystem() =>
        "Role: You are a strict continuity checker. You do NOT rewrite anything.\n"
        + "Objective: For each checklist item, decide only whether the chapter is correct (ok) or violates it (not ok), and give a one-line reason.\n"
        + "The world, the narrative frame (point of view, tense), the initial state, the story so far and the knowledge base are inviolable facts. Judge the chapter against them, never the other way around. Check facts and numbers against the story so far and the established facts, not only within this chapter.\n"
        + "Constraints: Work in English only. Do not propose fixes, do not quote long passages. Base every verdict strictly on the material given.\n"
        + "Output: Only a JSON object that matches the required schema: one entry per checklist id.";

    public static string CheckerGather() =>
        "Consult the project with the tools to check the chapter against the facts (characters, knowledge, world "
        + "state, recent loglines, search). When you have what you need, reply with exactly \"Ready.\" and nothing else.";

    public static IReadOnlyList<LlmMessage> BuildCheckerMessages(
        IContextAssembler assembler,
        WriterContext context,
        string text,
        IReadOnlyList<EditorCheck> checks)
    {
        var user = new StringBuilder();
        user.AppendLine(assembler.BuildStoryContext(context));

        user.AppendLine();
        user.AppendLine("Checklist — answer every item by id (ok = true, violation = false):");
        foreach (var check in checks)
        {
            user.AppendLine($"- {check.Id}: {check.Question}");
        }

        user.AppendLine();
        user.AppendLine("Chapter prose:");
        const int proseBudget = 16000;
        var prose = text.Trim();
        user.AppendLine(prose.Length <= proseBudget ? prose : prose[..proseBudget] + "…[truncated]");

        user.AppendLine();
        user.AppendLine("Return one object per checklist id with { id, ok, reason }. Use ok = false only for a real violation.");

        return [LlmMessage.System(CheckerSystem()), LlmMessage.User(user.ToString())];
    }

    public static IReadOnlyList<LlmMessage> BuildFixerMessages(
        IContextAssembler assembler,
        WriterContext context,
        EditorCheck check,
        string reason)
    {
        var system = "Role: You are a meticulous continuity fixer.\n"
            + $"Objective: Fix the single \"{check.Label}\" problem described below and change nothing else.\n"
            + "The world, the narrative frame, the initial state, the story so far and the knowledge base are inviolable. Correct the prose to match the facts, never the other way around.\n"
            + "Constraints: Change ONLY what the described problem requires. Keep every other sentence, story beat, character detail, and fact exactly as it is — do not rephrase, shorten, expand, reorder or 'improve' anything that is not part of the problem. NEVER change a character's name, a form of address or a vocative (for example \"Elara?\" or \"Elara, you have come far\"): names are not the problem. Preserve the author's voice, the point of view, the tense and the full length. Write in English only. Never mention chapter numbers or the book itself. Return a COMPLETE chapter, not a fragment.\n"
            + "Output: Only the full revised chapter prose — no title, headings or commentary.";

        var user = new StringBuilder();
        user.AppendLine(assembler.BuildStoryContext(context));
        user.AppendLine();
        user.AppendLine($"Problem to fix ({check.Id}): {reason}");
        user.AppendLine(check.FixInstruction);
        user.AppendLine();
        user.AppendLine("Apply the smallest possible change that removes this problem. Leave every other word of the chapter untouched.");

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }

    public static string FixerGather() =>
        "Consult the project with the tools to confirm the established facts (characters, knowledge, world state, "
        + "recent loglines, search) before fixing. When you have what you need, reply with exactly \"Ready.\" and nothing else.";

    public static IReadOnlyList<LlmMessage> BuildCosmeticMessages(IContextAssembler assembler, WriterContext context)
    {
        var system = "Role: You are a meticulous fiction editor.\n"
            + "Objective: Polish the chapter draft for prose quality, pacing, repetition, clarity and style.\n"
            + "Constraints: Preserve the author's voice, the point of view, the tense, the established facts and "
            + "every story beat. Keep the draft's full length and detail — never summarize or shorten it. Write in "
            + "English only. Never mention chapter numbers or the book itself.\n"
            + "Output: Only the revised chapter prose — no title, headings or commentary.\n"
            + "Do not revise until you are asked to.";

        return [LlmMessage.System(system), LlmMessage.User(assembler.BuildStoryContext(context))];
    }

    public static string CosmeticSystem() =>
        "Role: You are a meticulous fiction editor.\n"
        + "Objective: Polish the chapter draft for prose quality, pacing, repetition, clarity and style.\n"
        + "Constraints: Preserve the author's voice, the point of view, the tense, the established facts and "
        + "every story beat. Keep the draft's full length and detail — never summarize or shorten it. Write in "
        + "English only. Never mention chapter numbers or the book itself.\n"
        + "Output: Only the revised chapter prose — no title, headings or commentary.\n"
        + "Do not revise until you are asked to.";
}
