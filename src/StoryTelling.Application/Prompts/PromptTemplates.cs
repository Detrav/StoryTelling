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

        if (request.Snapshot is { Knowledge.Count: > 0 } snapshot)
        {
            user.AppendLine();
            user.AppendLine("Knowledge base (fetch details with the tools):");
            const int manifestCap = 120;
            foreach (var entry in snapshot.Knowledge.Take(manifestCap))
            {
                user.AppendLine($"- [{entry.Kind}] {entry.Title}");
            }

            if (snapshot.Knowledge.Count > manifestCap)
            {
                user.AppendLine($"- …and {snapshot.Knowledge.Count - manifestCap} more (use the tools to list them)");
            }
        }

        if (request.Target == GenerationTarget.ChapterSettings && request.Snapshot is { } project)
        {
            AppendPreviousChapters(user, project);
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

        if (request.Avoid.Count > 0)
        {
            user.AppendLine();
            user.AppendLine("These entries already exist. Do not create another entry with the same name, and make new given names clearly distinct from these:");
            foreach (var name in request.Avoid)
            {
                user.AppendLine($"- {name}");
            }

            user.AppendLine("Exception: if the author's brief places the new character in an existing family or group, keep the shared family name — only the given name must differ.");
        }

        if (useTools)
        {
            user.AppendLine("Consult the project with the tools (characters, initial world state, knowledge entries, search) before answering; prefer checking the project over guessing.");
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
        var system = "You are a meticulous story-bible reviewer. Review the knowledge base for internal "
            + "inconsistencies, contradictions, gaps and unclear points. Do not rewrite anything — only "
            + "report findings. Use the provided tools to read the details. Reply with ONLY a JSON object "
            + "that matches the required schema.";

        var user = new StringBuilder();
        user.AppendLine("Review the knowledge base for consistency and gaps. Read the details with the tools.");
        user.AppendLine();
        user.AppendLine("Manifest:");
        AppendField(user, "Book name", snapshot.Name);

        if (snapshot.Knowledge.Count > 0)
        {
            user.AppendLine($"- Knowledge: {string.Join(", ", snapshot.Knowledge.Select(entry => $"{entry.Title} [{entry.Kind}]"))}");
        }

        user.AppendLine();
        user.AppendLine("Report each problem as a finding: a severity (Info, Warning or Error), the area "
            + "(Knowledge or General), a short title, a concrete detail (what is inconsistent or missing, "
            + "and where), and an optional suggestion. If the knowledge base is consistent, return an empty "
            + "list of findings.");
        user.AppendLine();
        user.AppendLine("Set reference to the exact title of the knowledge entry a finding is about. Always "
            + "fill it for knowledge findings; leave it empty for whole-project issues.");
        user.AppendLine("Prefer providing a fix whenever the problem is corrected by replacing one or more "
            + "field values: add an edit for each changed field with target Knowledge, reference (the entry "
            + "title) and the corrected value. Use the exact field names below. Only omit the fix when no "
            + "field-level correction makes sense. Fields per target:");
        user.AppendLine("- Knowledge: Kind, Title, Tags, Content");

        if (!string.IsNullOrWhiteSpace(brief))
        {
            user.AppendLine($"Focus: {brief.Trim()}");
        }

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }

    private static string LabelFor(string key) => _projectLabels.TryGetValue(key, out var label) ? label : key;

    private static void AppendPreviousChapters(StringBuilder user, Project project)
    {
        var previous = project.Chapters
            .Where(chapter => !string.IsNullOrWhiteSpace(chapter.Logline))
            .OrderBy(chapter => chapter.Number)
            .TakeLast(5)
            .ToList();

        if (previous.Count > 0)
        {
            user.AppendLine();
            user.AppendLine("Previous chapters (oldest first):");
            foreach (var chapter in previous)
            {
                user.AppendLine($"- Chapter {chapter.Number} (\"{chapter.Title.Trim()}\"): {chapter.Logline.Trim()}");
            }
        }

        var state = project.Chapters.LastOrDefault(chapter => !string.IsNullOrWhiteSpace(chapter.ContentOriginal))?.WorldState
            ?? project.InitialWorldState;
        user.AppendLine();
        user.AppendLine("Situation after the previous chapter:");
        AppendField(user, "Time and place", state.TimeAndPlace);
        if (!string.IsNullOrWhiteSpace(state.Description))
        {
            user.AppendLine(state.Description.Trim());
        }
    }

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
        user.AppendLine("Pick kind from: Note, Character, Place, Item, Event, Faction, Rule, Background. Use Note when unsure.");
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

    public static IReadOnlyList<LlmMessage> DesignKnowledge(string description, string brief)
    {
        var system = "You design the initial knowledge base for a story from the author's description. "
            + "Produce a coherent set of entries — characters, places, factions, items, events, rules and "
            + "background — inventing specific names, details and relationships that fit the description and "
            + "stay consistent with each other. Prefer a handful of well-developed entries over many thin "
            + "ones. Do not duplicate an entry. Work in English only. Reply with ONLY a JSON object that "
            + "matches the required schema.";

        var user = new StringBuilder();
        user.AppendLine("Design the knowledge base from this description. Return one entry per distinct character, place, faction, item, event, rule or background fact.");
        user.AppendLine("Pick kind from: Note, Character, Place, Item, Event, Faction, Rule, Background. Use Note when unsure.");
        user.AppendLine("Give each entry a short title, a few short tags, and a self-contained content body.");

        if (!string.IsNullOrWhiteSpace(brief))
        {
            user.AppendLine($"Author's brief: {brief.Trim()}");
        }

        user.AppendLine();
        user.AppendLine("Description:");
        user.AppendLine(description);

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }

    public static string WriterSystem() =>
        "You write the chapters of a multi-chapter story in English only. You may consult the project "
        + "with the provided tools. Everything must stay consistent with the world, world state and "
        + "knowledge you find. Do not write the chapter until you are asked to.";

    public static string WriterGather() =>
        "Consult the project with the tools to refresh the facts you need (characters, initial world "
        + "state, recent loglines, knowledge, search). When you have what you need, reply with one short "
        + "line; do not write the chapter yet.";

    public static string WriterWrite(Chapter chapter) =>
        $"Now write chapter {chapter.Number}"
        + (string.IsNullOrWhiteSpace(chapter.Title) ? string.Empty : $" (\"{chapter.Title.Trim()}\")")
        + " as a full chapter of roughly 1500-2500 words. Output only the chapter prose in English — "
        + "no headings, notes or commentary.";

    public static string WriterBrief(Chapter chapter)
    {
        var lines = new List<string> { $"Chapter {chapter.Number}: {chapter.Title.Trim()}".TrimEnd() };
        AddLine(lines, "Direction", chapter.Direction);
        AddLine(lines, "Notes", chapter.Notes);
        return string.Join("\n", lines);
    }

    public static string WriterWorldStyle(World world)
    {
        var lines = new List<string>();
        AddLine(lines, "Genre", world.Genre);
        AddLine(lines, "Tone", world.Tone);
        AddLine(lines, "Style", world.Style);
        AddLine(lines, "Point of view", world.PointOfView);
        AddLine(lines, "Tense", world.Tense);
        AddLine(lines, "Rating", world.Rating);
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

    public static string WriterWorldLore(World world)
    {
        if (string.IsNullOrWhiteSpace(world.Title) && string.IsNullOrWhiteSpace(world.Body))
        {
            return string.Empty;
        }

        var builder = new StringBuilder("World: ");
        builder.Append(string.IsNullOrWhiteSpace(world.Title) ? "unnamed" : world.Title.Trim());
        if (!string.IsNullOrWhiteSpace(world.Body))
        {
            builder.Append('\n').Append(world.Body.Trim());
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

        var cast = project.Knowledge.Where(entry => entry.Kind == KnowledgeKind.Character && !string.IsNullOrWhiteSpace(entry.Title)).ToList();
        if (cast.Count > 0)
        {
            lines.Add($"- Cast: {string.Join(", ", cast.Select(entry => entry.Title.Trim()))}");
        }

        var other = project.Knowledge.Where(entry => entry.Kind != KnowledgeKind.Character).ToList();
        if (other.Count > 0)
        {
            lines.Add($"- Knowledge: {string.Join(", ", other.Select(entry => $"{entry.Title} [{entry.Kind}]"))}");
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

    public static IReadOnlyList<LlmMessage> BuildSummarizer(Chapter chapter, WorldState stateBefore, IReadOnlyList<KnowledgeEntry> knowledge)
    {
        var system = "You summarize a finished story chapter so later chapters stay consistent. Work in "
            + "English only. Reply with ONLY a JSON object that matches the required schema.";

        var user = new StringBuilder();
        user.AppendLine($"Summarize chapter {chapter.Number}"
            + (string.IsNullOrWhiteSpace(chapter.Title) ? string.Empty : $" (\"{chapter.Title.Trim()}\")")
            + ".");
        user.AppendLine();
        user.AppendLine("World state before this chapter:");
        AppendField(user, "Time and place", stateBefore.TimeAndPlace);
        if (!string.IsNullOrWhiteSpace(stateBefore.Description))
        {
            user.AppendLine($"- Situation: {stateBefore.Description.Trim()}");
        }

        if (knowledge.Count > 0)
        {
            user.AppendLine();
            user.AppendLine("Current knowledge base (use the exact title to update or delete an entry):");
            const int perEntry = 2000;
            const int total = 16000;
            var used = 0;
            foreach (var entry in knowledge)
            {
                var tags = entry.Tags.Count > 0 ? $" [{string.Join(", ", entry.Tags)}]" : string.Empty;
                user.AppendLine($"- [{entry.Kind}] {entry.Title}{tags}");
                if (string.IsNullOrWhiteSpace(entry.Content))
                {
                    continue;
                }

                var room = Math.Min(perEntry, total - used);
                if (room <= 0)
                {
                    continue;
                }

                var text = Truncate(entry.Content.Trim(), room);
                used += text.Length;
                user.AppendLine(text);
            }
        }

        user.AppendLine();
        user.AppendLine("Chapter text:");
        user.AppendLine(chapter.ContentOriginal);
        user.AppendLine();
        user.AppendLine("Reply with a logline (1-2 sentences on what actually happened), the new time and "
            + "place, a description of the situation AFTER this chapter, and the knowledge changes caused "
            + "by it. Base everything strictly on the chapter text.");
        user.AppendLine();
        user.AppendLine("When you update an entry, output its full updated content and keep every detail "
            + "from its current content that the chapter does not contradict.");
        user.AppendLine();
        user.AppendLine("knowledgeChanges lists only entries that actually changed: operation (Create, "
            + "Update or Delete), the title (for Update/Delete use the exact existing title), kind, tags, "
            + "the full new content, and a short reason. Return an empty array when nothing changed — do "
            + "not restate unchanged entries.");

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }

    private static string Truncate(string text, int max)
    {
        if (text.Length <= max)
        {
            return text;
        }

        return text[..max] + "…[truncated]";
    }

    public static IReadOnlyList<LlmMessage> BuildTranslation(string text, string languageCode)
    {
        var system = "You translate fiction between languages. Preserve the meaning, tone, style, "
            + "character names and formatting, and keep the same paragraphs and line breaks. Output "
            + "only the translation in the target language — no notes or commentary.";

        var user = new StringBuilder();
        user.AppendLine($"Translate the following chapter into the language with the ISO code \"{languageCode.Trim()}\".");
        user.AppendLine();
        user.AppendLine(text);

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }

    public static string EditorSystem() =>
        "You are a meticulous fiction editor. Preserve the author's voice, the story world and the "
        + "established facts. Work in English only. Do not revise until you are asked to.";

    public static string EditorGather() =>
        "Consult the project with the tools to check continuity and the established facts (characters, "
        + "knowledge, world state, recent loglines) before revising. When you have what you need, reply "
        + "with one short line; do not revise yet.";

    public static IReadOnlyList<LlmMessage> BuildEditorSeed(Chapter chapter, WorldState stateBefore, Project project)
    {
        var user = new StringBuilder();
        user.AppendLine(WriterBrief(chapter));

        var frame = WriterWorldStyle(project.World);
        if (frame.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(frame);
        }

        var state = WriterState(stateBefore);
        if (state.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(state);
        }

        user.AppendLine();
        user.AppendLine(WriterManifest(project, chapter));

        return [LlmMessage.System(EditorSystem()), LlmMessage.User(user.ToString())];
    }

    public static IReadOnlyList<LlmMessage> BuildEditorWrite(string draft)
    {
        var user = new StringBuilder();
        user.AppendLine("Revise the chapter draft below for continuity, pacing, repetition, clarity and style. "
            + "Preserve the author's voice and the established facts. Keep the revision at the draft's full "
            + "length and detail — never summarize or shorten it. Output only the revised chapter text, in "
            + "English, with no notes or commentary.");
        user.AppendLine();
        user.AppendLine("Draft to revise:");
        user.AppendLine(draft);
        user.AppendLine();
        user.AppendLine("Return the full revised chapter text.");
        return [LlmMessage.User(user.ToString())];
    }

    public static IReadOnlyList<LlmMessage> BuildEditorNotes(string changes)
    {
        var system = "You summarize the edits made by a fiction editor. Work in English only. Reply with "
            + "ONLY a JSON object that matches the required schema.";

        var user = new StringBuilder();
        user.AppendLine("List the meaningful changes the editor made, based on these before/after excerpts. "
            + "Give a kind (Continuity, Style, Pacing, Repetition, Clarity or Other) and a short note for "
            + "each. Return an empty list when nothing meaningful changed.");
        user.AppendLine();
        user.AppendLine("Changes:");
        user.AppendLine(changes);

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }
}
