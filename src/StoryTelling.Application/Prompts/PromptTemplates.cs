using System.Text;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;

namespace StoryTelling.Application.Prompts;

public static class PromptTemplates
{
    private static readonly Dictionary<string, string> _projectLabels = new()
    {
        ["ProjectName"] = "Book name",
        ["WorldTitle"] = "World title",
        ["WorldBody"] = "World description",
        ["Genre"] = "Genre",
        ["Tone"] = "Tone",
        ["Style"] = "Style",
        ["PointOfView"] = "Point of view",
        ["Tense"] = "Tense",
        ["Rating"] = "Rating",
        ["Premise"] = "Premise",
        ["Direction"] = "Direction",
    };

    public static IReadOnlyList<LlmMessage> Build(GenerationRequest request, bool useTools = false)
    {
        var system = "You help outline a multi-chapter story. Work in English only. "
            + (useTools ? "Use the provided tools to consult the project before answering. " : string.Empty)
            + $"Reply with exactly {request.Variants} distinct options that match the required JSON schema — "
            + "no prose, no explanations.";

        var specs = GenerationTargets.Fields(request.Target);
        var ownFields = specs.Select(spec => spec.Field).ToHashSet();
        var fields = request.Context.Fields;

        var user = new StringBuilder();
        user.AppendLine("Current project:");

        var wroteConstraint = false;
        foreach (var (key, value) in fields)
        {
            if (ownFields.Contains(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            wroteConstraint = true;
            user.AppendLine($"- {LabelFor(key)}: {value.Trim()}");
        }

        if (!wroteConstraint)
        {
            user.AppendLine("- (nothing yet)");
        }

        if (request.Context.Cast.Count > 0)
        {
            user.AppendLine();
            user.AppendLine("Characters in the story:");
            foreach (var character in request.Context.Cast)
            {
                user.AppendLine($"- {character}");
            }
        }

        if (request.Snapshot is { Knowledge.Count: > 0 } snapshot)
        {
            user.AppendLine();
            user.AppendLine("Knowledge base (fetch details with the tools):");
            foreach (var entry in snapshot.Knowledge)
            {
                user.AppendLine($"- [{entry.Kind}] {entry.Title}");
            }
        }

        var draft = specs
            .Where(spec => fields.TryGetValue(spec.Field, out var value) && !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (draft.Count > 0)
        {
            user.AppendLine();
            user.AppendLine("Current draft (improve it or replace it entirely):");
            foreach (var spec in draft)
            {
                user.AppendLine($"- {spec.Label}: {fields[spec.Field].Trim()}");
            }
        }

        user.AppendLine();
        user.AppendLine(GenerationTargets.Instruction(request.Target));

        if (useTools)
        {
            user.AppendLine("Consult the project with the tools (characters, world state, knowledge entries, search) before answering; prefer checking the project over guessing.");
        }

        if (!string.IsNullOrWhiteSpace(request.Brief))
        {
            user.AppendLine($"Author's brief: {request.Brief.Trim()}");
        }

        user.AppendLine($"Provide exactly {request.Variants} distinct options.");

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }

    private static string LabelFor(string key) => _projectLabels.TryGetValue(key, out var label) ? label : key;

    public static IReadOnlyList<LlmMessage> ExtractKnowledge(string chunk, string brief)
    {
        var system = "You convert arbitrary source material (rules, campaign notes, world or game descriptions) "
            + "into structured knowledge entries for a story wiki. Work in English only. "
            + "Reply with ONLY a JSON object that matches the required schema.";

        var user = new StringBuilder();
        user.AppendLine("Extract every distinct entity or fact from the text below as one knowledge entry each.");
        user.AppendLine("Pick kind from: Note, Place, Item, Event, Faction, Rule, Background. Use Note when unsure.");
        user.AppendLine("Give each entry a short title, a few short tags, and a self-contained content body that keeps the important details.");

        if (!string.IsNullOrWhiteSpace(brief))
        {
            user.AppendLine($"Author's brief: {brief.Trim()}");
        }

        user.AppendLine();
        user.AppendLine("Text:");
        user.AppendLine(chunk);

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }
}
