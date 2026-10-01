using System.Text;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Domain;

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

    public static IReadOnlyList<LlmMessage> BuildReview(Project snapshot, string brief)
    {
        var system = "You are a meticulous story-bible reviewer. Review the project for internal "
            + "inconsistencies, contradictions, gaps and unclear points across the frame, world, "
            + "characters, knowledge and world state. Do not rewrite anything — only report findings. "
            + "Use the provided tools to read the details. Reply with ONLY a JSON object that matches "
            + "the required schema.";

        var user = new StringBuilder();
        user.AppendLine("Review the project for consistency and gaps. Read the details with the tools.");
        user.AppendLine();
        user.AppendLine("Manifest:");
        AppendField(user, "Book name", snapshot.Name);
        AppendField(user, "Genre", snapshot.Frame.Genre);
        AppendField(user, "Tone", snapshot.Frame.Tone);
        AppendField(user, "Style", snapshot.Frame.Style);
        AppendField(user, "Point of view", snapshot.Frame.PointOfView);
        AppendField(user, "Tense", snapshot.Frame.Tense);
        AppendField(user, "Rating", snapshot.Frame.Rating);
        AppendField(user, "Premise", snapshot.Frame.Premise);
        AppendField(user, "Direction", snapshot.Frame.Direction);
        AppendField(user, "World title", snapshot.Lore.Title);
        AppendField(user, "World state", snapshot.WorldState.TimeAndPlace);

        if (snapshot.Characters.Count > 0)
        {
            user.AppendLine($"- Characters: {string.Join(", ", snapshot.Characters.Select(character => character.Name))}");
        }

        if (snapshot.Knowledge.Count > 0)
        {
            user.AppendLine($"- Knowledge: {string.Join(", ", snapshot.Knowledge.Select(entry => $"{entry.Title} [{entry.Kind}]"))}");
        }

        user.AppendLine();
        user.AppendLine("Report each problem as a finding: a severity (Info, Warning or Error), the area "
            + "(Frame, World, Characters, Knowledge, WorldState, Languages or General), a short title, a "
            + "concrete detail (what is inconsistent or missing, and where), and an optional suggestion. "
            + "If the project is consistent, return an empty list of findings.");
        user.AppendLine();
        user.AppendLine("Set reference to the exact name of the character or the exact title of the knowledge "
            + "entry a finding is about. Always fill it for character or knowledge findings; leave it empty "
            + "for whole-project, frame, world or world-state issues.");
        user.AppendLine("Prefer providing a fix whenever the problem is corrected by replacing one or more "
            + "field values: add an edit for each changed field with target, reference (character name or "
            + "entry title, otherwise empty), field and value (the corrected text). Use the exact field names "
            + "below. Only omit the fix when no field-level correction makes sense. Fields per target:");
        user.AppendLine("- ProjectName: ProjectName");
        user.AppendLine("- World: WorldTitle, WorldBody");
        user.AppendLine("- Frame: Genre, Tone, Style, PointOfView, Tense, Rating, Premise, Direction");
        user.AppendLine("- WorldState: TimeAndPlace, Description");
        user.AppendLine("- Character: Name, Role, Age, Description, Personality, Background, Goals, Traits");
        user.AppendLine("- Knowledge: Kind, Title, Tags, Content");

        if (!string.IsNullOrWhiteSpace(brief))
        {
            user.AppendLine($"Focus: {brief.Trim()}");
        }

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }

    private static string LabelFor(string key) => _projectLabels.TryGetValue(key, out var label) ? label : key;

    private static void AppendField(StringBuilder builder, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"- {label}: {value.Trim()}");
        }
    }

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

    public static string WriterSystem() =>
        "You write the chapters of a multi-chapter story in English only. You may consult the project "
        + "with the provided tools. Everything must stay consistent with the frame, world state, "
        + "characters and knowledge you find. Do not write the chapter until you are asked to.";

    public static string WriterGather() =>
        "Consult the project with the tools to refresh the facts you need (characters, world state, "
        + "recent loglines, knowledge, search). When you have what you need, reply with one short line; "
        + "do not write the chapter yet.";

    public static string WriterWrite(Chapter chapter) =>
        $"Now write chapter {chapter.Number}"
        + (string.IsNullOrWhiteSpace(chapter.Title) ? string.Empty : $" (\"{chapter.Title.Trim()}\")")
        + ". Output only the chapter prose in English — no headings, notes or commentary.";

    public static string WriterBrief(Chapter chapter)
    {
        var lines = new List<string> { $"Chapter {chapter.Number}: {chapter.Title.Trim()}".TrimEnd() };
        AddLine(lines, "Direction", chapter.Direction);
        AddLine(lines, "Notes", chapter.Notes);
        return string.Join("\n", lines);
    }

    public static string WriterFrame(StoryFrame frame)
    {
        var lines = new List<string>();
        AddLine(lines, "Genre", frame.Genre);
        AddLine(lines, "Tone", frame.Tone);
        AddLine(lines, "Style", frame.Style);
        AddLine(lines, "Point of view", frame.PointOfView);
        AddLine(lines, "Tense", frame.Tense);
        AddLine(lines, "Rating", frame.Rating);
        return lines.Count == 0 ? string.Empty : "Frame:\n" + string.Join("\n", lines);
    }

    public static string WriterState(WorldState state)
    {
        var lines = new List<string>();
        AddLine(lines, "Time and place", state.TimeAndPlace);
        if (!string.IsNullOrWhiteSpace(state.Description))
        {
            lines.Add(state.Description.Trim());
        }

        return lines.Count == 0 ? string.Empty : "World state before this chapter:\n" + string.Join("\n", lines);
    }

    public static string WriterPremise(Project project)
    {
        var lines = new List<string>();
        AddLine(lines, "Premise", project.Frame.Premise);
        AddLine(lines, "Overall direction", project.Frame.Direction);
        return lines.Count == 0 ? string.Empty : string.Join("\n", lines);
    }

    public static string WriterLore(WorldLore lore)
    {
        if (string.IsNullOrWhiteSpace(lore.Title) && string.IsNullOrWhiteSpace(lore.Body))
        {
            return string.Empty;
        }

        var builder = new StringBuilder("World: ");
        builder.Append(string.IsNullOrWhiteSpace(lore.Title) ? "unnamed" : lore.Title.Trim());
        if (!string.IsNullOrWhiteSpace(lore.Body))
        {
            builder.Append('\n').Append(lore.Body.Trim());
        }

        return builder.ToString();
    }

    public static string WriterManifest(Project project, Chapter chapter)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(project.Name))
        {
            lines.Add($"- Book: {project.Name.Trim()}");
        }

        lines.Add($"- Chapters: {project.Chapters.Count} (writing chapter {chapter.Number})");

        if (project.Characters.Count > 0)
        {
            lines.Add($"- Cast: {string.Join(", ", project.Characters.Select(character => string.IsNullOrWhiteSpace(character.Role) ? character.Name : $"{character.Name} ({character.Role})"))}");
        }

        if (project.Knowledge.Count > 0)
        {
            lines.Add($"- Knowledge: {string.Join(", ", project.Knowledge.Select(entry => $"{entry.Title} [{entry.Kind}]"))}");
        }

        return "Project manifest:\n" + string.Join("\n", lines);
    }

    private static void AddLine(List<string> lines, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"- {label}: {value.Trim()}");
        }
    }
}
