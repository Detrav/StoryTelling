using System.Text;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Llm;
using StoryTelling.Domain;

namespace StoryTelling.Application.Prompts;

public static class EditorVerdictPrompt
{
    public static IReadOnlyList<LlmMessage> Build(
        Project project,
        Chapter chapter,
        string revised,
        WorldState stateBefore,
        IReadOnlyList<KnowledgeEntry> knowledge,
        IReadOnlyList<EditorIssue>? knownIssues)
    {
        var system = "Role: You are a strict story-continuity checker.\n"
            + "Objective: Verify that one finished chapter does not contradict the world, the initial state or the knowledge base.\n"
            + "Method: Compare the chapter prose against the facts. A character listed as alive must not be treated as dead; ages, relations and stated facts must match the entries; the world's fixed rules must hold.\n"
            + "Constraints: Work in English only. Report only real contradictions; ignore style, pacing and taste. Base every issue strictly on the material given.\n"
            + "Output: Only a JSON object that matches the required schema. Return an empty issues list when the chapter is consistent.";

        var user = new StringBuilder();
        user.AppendLine($"Chapter {chapter.Number}"
            + (string.IsNullOrWhiteSpace(chapter.Title) ? string.Empty : $" (\"{chapter.Title.Trim()}\")")
            + ".");
        user.AppendLine();
        user.AppendLine("World (inviolable canon):");
        user.AppendLine(string.IsNullOrWhiteSpace(project.World.Title) ? "(unnamed)" : project.World.Title.Trim());
        if (!string.IsNullOrWhiteSpace(project.World.Body))
        {
            user.AppendLine(project.World.Body.Trim());
        }

        user.AppendLine();
        user.AppendLine("Initial state:");
        user.AppendLine(string.IsNullOrWhiteSpace(stateBefore.TimeAndPlace) ? "(no time and place)" : stateBefore.TimeAndPlace.Trim());
        if (!string.IsNullOrWhiteSpace(stateBefore.Situation))
        {
            user.AppendLine(stateBefore.Situation.Trim());
        }

        user.AppendLine();
        user.AppendLine("Established facts:");
        const int budget = 12000;
        var used = 0;
        foreach (var entry in knowledge)
        {
            var line = $"- [{entry.Kind}] {entry.Title}: {entry.Content?.Trim()}";
            var room = Math.Min(line.Length, budget - used);
            if (room <= 0)
            {
                break;
            }

            user.AppendLine(line[..room]);
            used += room;
        }

        if (knownIssues is { Count: > 0 })
        {
            user.AppendLine();
            user.AppendLine("Issues flagged before the rewrite (verify they are resolved):");
            foreach (var issue in knownIssues)
            {
                user.AppendLine($"- [{issue.Severity}] {issue.Detail} (reference: {issue.Reference})");
            }
        }

        user.AppendLine();
        user.AppendLine("Chapter prose:");
        const int proseBudget = 16000;
        var prose = revised.Trim();
        user.AppendLine(prose.Length <= proseBudget ? prose : prose[..proseBudget] + "…[truncated]");

        user.AppendLine();
        user.AppendLine("List each contradiction as severity (Info, Warning or Error), detail and the exact entry title as reference. "
            + "Use Error only for a hard canon break. Return an empty list when the chapter is consistent.");

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }
}
