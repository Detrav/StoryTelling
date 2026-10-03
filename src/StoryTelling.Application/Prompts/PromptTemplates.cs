using System.Text;
using System.Text.RegularExpressions;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Review;
using StoryTelling.Application.Translation;
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
        var system = "Role: You are the story architect who plans and populates a multi-chapter book.\n"
            + "Objective: Produce options that fit the current project and the author's brief.\n"
            + "Constraints: Work in English only. "
            + (useTools ? "Consult the project with the provided tools before answering. " : string.Empty)
            + "Respect everything already established.\n"
            + (request.Bundle
                ? $"Output: Exactly {request.Variants} sequential items matching the required JSON schema — no prose, no explanations."
                : $"Output: Exactly {request.Variants} distinct options matching the required JSON schema — no prose, no explanations.");

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

        if (request.Target is GenerationTarget.ChapterSettings or GenerationTarget.Finale && request.Snapshot is { } project)
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
        user.AppendLine(request.Instruction ?? GenerationTargets.Instruction(request.Target));

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

        if (request.Target == GenerationTarget.ChapterPlan)
        {
            user.AppendLine();
            user.AppendLine($"Plan exactly {request.Variants} chapters in reading order. These are sequential "
                + "chapters of ONE story, not alternative options: chapter 1 happens first, chapter 2 continues "
                + "it, and so on. Do not number or name other chapters inside a chapter's direction and do not "
                + "describe the whole arc in every direction — each direction describes only that chapter and "
                + "ends where the next one begins.");
            user.AppendLine($"Spread the story across them (setup, rising action, climax, resolution) and make "
                + $"sure it reaches a FULL resolution in chapter {request.Variants}: the last chapter must "
                + "resolve every open thread — no cliffhanger, no new mystery, nothing left for a sequel.");
        }

        if (request.Target == GenerationTarget.Finale)
        {
            user.AppendLine();
            user.AppendLine("This is the FINAL chapter. Resolve every open thread and end the story — no "
                + "cliffhanger and no setup for a sequel.");
        }

        if (!string.IsNullOrWhiteSpace(request.Brief))
        {
            user.AppendLine($"Author's brief: {request.Brief.Trim()}");
        }

        user.AppendLine(request.Bundle
            ? $"Provide exactly {request.Variants} sequential items."
            : $"Provide exactly {request.Variants} distinct options.");

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }

    public static IReadOnlyList<LlmMessage> BuildReview(Project snapshot, string brief, ReviewCheck check)
    {
        var system = "Role: You are a meticulous story-bible continuity editor.\n"
            + "Objective: Find every internal inconsistency, contradiction, gap and ambiguity in the story bible — the world and the knowledge base.\n"
            + "Method: Compare the entries of the story bible against each other. Never judge an entry in isolation.\n"
            + "Constraints: Work in English only. Base every finding strictly on the given facts; report problems instead of silently changing the bible.\n"
            + "Answer immediately: never write out your analysis, reasoning or working notes; the JSON object must be your only output.\n"
            + "Stop as soon as you have covered the checklist; do not keep hunting for more issues after you have recorded the serious ones.\n"
            + "Output: Only a JSON object that matches the required schema.";

        var user = new StringBuilder();
        user.AppendLine($"{check.Intro} You are given the relevant parts of the story bible below.");
        user.AppendLine();
        user.AppendLine("Story bible:");
        AppendField(user, "Book name", snapshot.Name);

        if (check.IncludeWorld)
        {
            var world = snapshot.World;
            AppendField(user, "World", world.Title);
            AppendField(user, "World description", world.Body);
            AppendField(user, "Genre", world.Genre);
            AppendField(user, "Tone", world.Tone);
            AppendField(user, "Point of view", world.PointOfView);
            AppendField(user, "Tense", world.Tense);
            AppendField(user, "Rating", world.Rating);
        }

        if (check.IncludeInitialState)
        {
            AppendField(user, "Opens at", snapshot.InitialWorldState.TimeAndPlace);
            AppendField(user, "Opening situation", snapshot.InitialWorldState.Situation);
        }

        var entries = (check.Kinds is { Count: > 0 }
                ? snapshot.Knowledge.Where(entry => check.Kinds.Contains(entry.Kind))
                : snapshot.Knowledge)
            .ToList();

        user.AppendLine();
        user.AppendLine($"Knowledge entries ({entries.Count}):");
        var used = 0;
        foreach (var entry in entries)
        {
            var text = check.IncludeFullText ? RenderEntry(entry) : SummaryLine(entry);
            if (used + text.Length > check.MaxChars)
            {
                user.AppendLine("- (remaining entries omitted to fit the budget)");
                break;
            }

            user.AppendLine(text);
            used += text.Length;
        }

        user.AppendLine();
        if (check.Reconcile)
        {
            if (HasDigit(user))
            {
                user.AppendLine("Reconcile the numbers: for each person, collect the ages, years, durations and ranks stated in the entries about them and compare them. Most contradictions hide across two different entries, so never check an entry only against itself.");
                user.AppendLine("Worked example of the required arithmetic: if one entry says \"Elena is 26\" (present day) and another says \"her son was 20 in 2019\", then Elena was 21 in 2019 and would have given birth at age 1 — that is impossible; report it as an Error. Compute the birth year of each relative from each statement and compare.");
                user.AppendLine("Hard rules: a biological parent must be at least 12 years older than their child; a person's age plus the years elapsed between two events must equal their stated age at the later event; a person cannot be the same age as their parent. Report the violations you find.");
                user.AppendLine();
                user.AppendLine("Optionally record the statements you compared in 'reconciliation' as one short line per person; skip it when there is nothing to reconcile.");
                user.AppendLine();
            }
            else
            {
                user.AppendLine("This story bible states no ages, dates, years, durations or ranks; do not attempt a timeline reconciliation.");
                user.AppendLine();
            }
        }

        user.AppendLine("Go through every category below and report the serious violations you find:");
        for (var index = 0; index < check.Checklist.Count; index++)
        {
            user.AppendLine($"{index + 1}. {check.Checklist[index]}");
        }

        user.AppendLine();
        user.AppendLine("Report at most 12 findings, ordered from the most to the least severe. Merge findings that describe the same underlying problem into one; do not restate the same issue under several titles. Keep each detail to one or two sentences — reporting the most serious problems matters more than listing everything. Ignore pure style or terminology preferences unless they create a real contradiction.");
        user.AppendLine();
        user.AppendLine("Report a problem as a finding: a severity (Info, Warning or Error), the area (Knowledge or General), a short title, a concrete detail (what is inconsistent or missing, and where), and an optional suggestion.");
        user.AppendLine("If the bible is consistent, return an empty list of findings.");
        user.AppendLine();
        user.AppendLine("Set reference to the exact title of the knowledge entry a finding is about. Always fill it for knowledge findings; leave it empty for whole-project issues.");
        user.AppendLine();
        user.AppendLine("Provide a 'fix' as an ordered list of operations when the correction is short and certain; leave 'fix' empty and use the 'suggestion' instead when it is a rewrite you cannot express. A fix may touch several entries at once — use that for renames, merges and splits.");
        user.AppendLine("Operations (target is always Knowledge):");
        user.AppendLine("- Set: reference (the entry title), field (Kind, Title, Tags or Content), value (the new full value). The value MUST differ from the current one.");
        user.AppendLine("- AddTag / RemoveTag: reference, value (a single tag).");
        user.AppendLine("- Create: reference (the new, unique entry title), kind, tags, value (the content body).");
        user.AppendLine("- Delete: reference (the entry title to remove).");
        user.AppendLine("Rename an entry: Set its Title and, in the same fix, Set the Content of every other entry that mentions the old title. Merge duplicates: Set the surviving entry with the merged Content and Tags, then Delete the others. Split an entry: Set the original with the trimmed Content and Create the new entry. Move misplaced content: Set the source and Set the destination in one fix.");
        user.AppendLine("A duplicate pair or a dangling/obsolete entry must always come with a fix — do not leave it for the reader to repair by hand.");
        user.AppendLine("List the operations in the order they must run. Do not invent facts to fill a gap. Do not return an operation that would change nothing.");

        if (!string.IsNullOrWhiteSpace(brief))
        {
            user.AppendLine($"Focus: {brief.Trim()}");
        }

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }

    private static bool HasDigit(StringBuilder builder)
    {
        for (var index = 0; index < builder.Length; index++)
        {
            if (char.IsDigit(builder[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static string SummaryLine(KnowledgeEntry entry)
    {
        var tags = entry.Tags.Count > 0 ? $" ({string.Join(", ", entry.Tags)})" : string.Empty;
        return $"- [{entry.Kind}] {entry.Title}{tags}";
    }

    private static string RenderEntry(KnowledgeEntry entry)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"[{entry.Kind}] {entry.Title}");
        if (entry.Tags.Count > 0)
        {
            builder.AppendLine($"Tags: {string.Join(", ", entry.Tags)}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Content))
        {
            builder.AppendLine(entry.Content.Trim());
        }

        return builder.ToString().TrimEnd();
    }

    public static IReadOnlyList<LlmMessage> BuildContinuityReview(Project project, Chapter chapter, IReadOnlyList<KnowledgeEntry> knowledge)
    {
        var system = "Role: You are a strict story-continuity checker.\n"
            + "Objective: Compare one chapter's drafted prose and its plan against the world, the initial state and the established facts, and report contradictions.\n"
            + "Method: Check the prose against the plan and the facts; check the facts against each other; check the prose against the inviolable world.\n"
            + "Constraints: Work in English only. Do not rewrite anything — only report. Base findings strictly on the given material. Ignore ordinary stylistic choices.\n"
            + "Output: Only a JSON object that matches the required schema.";

        var user = new StringBuilder();
        user.AppendLine($"Chapter {chapter.Number}"
            + (string.IsNullOrWhiteSpace(chapter.Title) ? string.Empty : $" (\"{chapter.Title.Trim()}\")")
            + " plan:");
        user.AppendLine(string.IsNullOrWhiteSpace(chapter.Direction) ? "- (no direction)" : $"- Direction: {chapter.Direction.Trim()}");
        if (!string.IsNullOrWhiteSpace(chapter.Notes))
        {
            user.AppendLine($"- Notes: {chapter.Notes.Trim()}");
        }

        user.AppendLine();
        user.AppendLine("World (inviolable canon):");
        user.AppendLine(string.IsNullOrWhiteSpace(project.World.Title) ? "(unnamed)" : project.World.Title.Trim());
        if (!string.IsNullOrWhiteSpace(project.World.Body))
        {
            user.AppendLine(project.World.Body.Trim());
        }

        user.AppendLine();
        user.AppendLine("Initial state:");
        user.AppendLine(string.IsNullOrWhiteSpace(project.InitialWorldState.TimeAndPlace) ? "(no time and place)" : project.InitialWorldState.TimeAndPlace.Trim());
        if (!string.IsNullOrWhiteSpace(project.InitialWorldState.Situation))
        {
            user.AppendLine(project.InitialWorldState.Situation.Trim());
        }

        user.AppendLine();
        user.AppendLine("Established facts:");
        const int knowledgeBudget = 12000;
        var used = 0;
        foreach (var entry in knowledge)
        {
            var line = $"- [{entry.Kind}] {entry.Title}: {entry.Content?.Trim()}";
            var room = Math.Min(line.Length, knowledgeBudget - used);
            if (room <= 0)
            {
                break;
            }

            user.AppendLine(line[..room]);
            used += room;
        }

        var openThreads = knowledge.Where(entry => entry.Kind == KnowledgeKind.Thread && entry.Status == KnowledgeStatus.Open).ToList();
        if (openThreads.Count > 0)
        {
            user.AppendLine();
            user.AppendLine("Open threads that should be tracked:");
            foreach (var thread in openThreads)
            {
                user.AppendLine($"- {thread.Title.Trim()}");
            }
        }

        user.AppendLine();
        user.AppendLine("Chapter prose:");
        const int proseBudget = 16000;
        var prose = chapter.ContentOriginal?.Trim() ?? string.Empty;
        user.AppendLine(prose.Length <= proseBudget ? prose : prose[..proseBudget] + "…[truncated]");

        user.AppendLine();
        user.AppendLine("Report findings where the prose or the plan contradicts the world, the initial state or "
            + "the facts — or where the prose fails to carry out the plan (wrong relationships, impossible ages, "
            + "alias mismatches, a dead character acting, a resolved fact reappearing, the world's fixed rules "
            + "broken, a promised event skipped). Severity is Info, Warning or Error; set reference to the exact "
            + "entry title involved. Return an empty list when everything is consistent.");

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
        if (!string.IsNullOrWhiteSpace(state.Situation))
        {
            user.AppendLine(state.Situation.Trim());
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
        "Role: You are a novelist writing one chapter of an ongoing book.\n"
        + "Objective: Extend the established story — never restart it — and carry out this chapter's task.\n"
        + "Constraints: Write in English only. Follow the given point of view and tense exactly. The "
        + "world description and the narrative frame (genre, tone, point of view, tense) are inviolable "
        + "canon: never contradict them, not even for atmosphere. Carry out the chapter direction you "
        + "are given; do not skip, summarize or replace it with a different scene. Stay consistent with "
        + "the world state and the knowledge. Never re-introduce people or places the reader has already "
        + "met. Never mention chapter numbers, the book, or these instructions in the prose. Aim for "
        + "roughly 1500-2500 words.\n"
        + "Output: Only the chapter prose — no title, headings or commentary.\n"
        + "Tools: Consult the project before writing. Do not write the chapter until you are asked to.";

    public static string WriterGather() =>
        "Consult the project with the tools to refresh the facts you need (characters, initial world "
        + "state, recent loglines, knowledge, search). When you have what you need, reply with exactly "
        + "\"Ready.\" and nothing else. Never write any part of the chapter in this phase.";

    public static string WriterWrite(Chapter chapter) =>
        $"Now write chapter {chapter.Number}"
        + (string.IsNullOrWhiteSpace(chapter.Title) ? string.Empty : $" (\"{chapter.Title.Trim()}\")")
        + " as a full chapter of roughly 1500-2500 words. Keep the frame's point of view and tense "
        + "exactly and stay within the world described above. Output only the chapter prose in English — "
        + "no headings, notes or commentary. Do not begin with the chapter title or a heading line; "
        + "start directly with the prose.";

    public static string WriterBrief(Chapter chapter)
    {
        var lines = new List<string> { $"Chapter {chapter.Number}: {chapter.Title.Trim()}".TrimEnd() };
        AddLine(lines, "Direction", DirectionFor(chapter.Direction));
        AddLine(lines, "Notes", chapter.Notes);
        return string.Join("\n", lines);
    }

    private static readonly Regex _chapterSentence = new(
        @"\bchapter\s+(?:\d+|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\b[^.!?]*[.!?]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string DirectionFor(string direction)
    {
        if (string.IsNullOrWhiteSpace(direction))
        {
            return direction;
        }

        var cleaned = _chapterSentence.Replace(direction, string.Empty);
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
        return cleaned.Length == 0 ? direction.Trim() : cleaned;
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
        if (!string.IsNullOrWhiteSpace(state.Situation))
        {
            lines.Add(state.Situation.Trim());
        }

        return lines.Count == 0 ? string.Empty : "World state before this chapter:\n" + string.Join("\n", lines);
    }

    public static string WriterPosition(Project project, Chapter chapter)
    {
        var total = project.Chapters.Count;
        var number = chapter.Number;
        var role = chapter.Role == ChapterRole.Auto ? DeriveRole(number, total) : chapter.Role;

        if (chapter.Role == ChapterRole.Auto && total <= 1)
        {
            return "This is the opening and the whole story: establish the setting, the characters and "
                + "the inciting incident, then bring it to a complete resolution within this chapter.";
        }

        if (role == ChapterRole.Opening)
        {
            return $"This is the opening chapter — the setup of the story (chapter {number} of {total}). "
                + "Establish the setting, introduce the characters and the inciting incident; do not "
                + "assume the reader knows anything yet. Leave clear threads to develop in later chapters.";
        }

        if (role == ChapterRole.Finale)
        {
            return $"This is the final chapter — the resolution of the story (chapter {number} of {total}). "
                + "Bring everything to a full close: resolve every open thread, pay off the setups, and do "
                + "not end on a cliffhanger or set up a sequel. The last paragraph must be a stable, closed "
                + "final note — no 'to be continued', no 'this is not the end', no promise that the "
                + "mystery/signal/conflict will return, and no new question left dangling.";
        }

        return $"This is chapter {number} of {total} — the middle of the story. The setup has already "
            + "happened; continue from the situation below instead of restarting, do not re-introduce "
            + "people or places the reader has already met, and keep moving toward the resolution.";
    }

    private static ChapterRole DeriveRole(int number, int total) =>
        number <= 1 ? ChapterRole.Opening : number >= total ? ChapterRole.Finale : ChapterRole.Middle;

    public static string WriterRecap(Project project, Chapter chapter, int recentCount)
    {
        var prior = project.Chapters
            .Where(candidate => candidate.Number < chapter.Number && !string.IsNullOrWhiteSpace(candidate.Logline))
            .OrderBy(candidate => candidate.Number)
            .ToList();

        if (prior.Count == 0)
        {
            return string.Empty;
        }

        var selected = new List<Chapter> { prior[0] };
        if (recentCount > 0)
        {
            foreach (var recent in prior.Skip(Math.Max(0, prior.Count - recentCount)))
            {
                if (selected.All(candidate => candidate.Number != recent.Number))
                {
                    selected.Add(recent);
                }
            }
        }

        selected = [.. selected.OrderBy(candidate => candidate.Number)];

        var lines = new List<string> { "Story so far:" };
        var previous = 0;
        foreach (var item in selected)
        {
            if (item.Number > previous + 1)
            {
                lines.Add($"- [Chapters {previous + 1}-{item.Number - 1} omitted — use the recent_loglines tool]");
            }

            lines.Add(DescribeLogline(item));
            previous = item.Number;
        }

        return string.Join("\n", lines);
    }

    private static string DescribeLogline(Chapter chapter) =>
        $"- Chapter {chapter.Number}"
        + (string.IsNullOrWhiteSpace(chapter.Title) ? string.Empty : $" (\"{chapter.Title.Trim()}\")")
        + $": {chapter.Logline.Trim()}";

    public static string OpenThreads(Project project, int cap = 10)
    {
        var threads = project.Knowledge
            .Where(entry => entry.Kind == KnowledgeKind.Thread && entry.Status == KnowledgeStatus.Open)
            .ToList();

        if (threads.Count == 0)
        {
            return string.Empty;
        }

        var pinned = threads.Where(thread => thread.Tags.Contains("pinned", StringComparer.OrdinalIgnoreCase)).ToList();
        var selected = pinned.Concat(threads.Where(thread => !pinned.Contains(thread))).Take(cap).ToList();

        var lines = new List<string> { "Open threads (unresolved):" };
        foreach (var thread in selected)
        {
            var detail = string.IsNullOrWhiteSpace(thread.Content) ? string.Empty : $": {thread.Content.Trim()}";
            lines.Add($"- {thread.Title.Trim()}{detail}");
        }

        if (threads.Count > selected.Count)
        {
            lines.Add($"- …and {threads.Count - selected.Count} more (use the list_entries tool with kind Thread)");
        }

        return string.Join("\n", lines);
    }

    public static string WriterCast(Project project)
    {
        var cast = project.Knowledge
            .Where(entry => entry.Kind == KnowledgeKind.Character && !string.IsNullOrWhiteSpace(entry.Title))
            .ToList();
        if (cast.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder("Cast:");
        foreach (var entry in cast)
        {
            builder.Append('\n').Append("- ").Append(entry.Title.Trim());
            if (entry.Tags.Count > 0)
            {
                builder.Append(" [").Append(string.Join(", ", entry.Tags)).Append(']');
            }

            if (!string.IsNullOrWhiteSpace(entry.Content))
            {
                builder.Append('\n').Append(entry.Content.Trim());
            }
        }

        return builder.ToString();
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

    public static IReadOnlyList<LlmMessage> BuildContinuityJudge(string storySoFar, WorldState situationBefore, Chapter chapter)
    {
        var system = "You are a strict story-continuity reviewer. Judge whether a chapter continues the "
            + "established story or reads like the start of a new one. Reply with ONLY a JSON object that "
            + "matches the required schema.";

        var user = new StringBuilder();
        user.AppendLine("Judge whether this chapter continues the story below.");
        user.AppendLine();
        user.AppendLine("Established story so far:");
        user.AppendLine(string.IsNullOrWhiteSpace(storySoFar) ? "- (this is the first chapter)" : storySoFar);
        user.AppendLine();
        user.AppendLine("Situation before this chapter:");
        AppendField(user, "Time and place", situationBefore.TimeAndPlace);
        if (!string.IsNullOrWhiteSpace(situationBefore.Situation))
        {
            user.AppendLine($"- Situation: {situationBefore.Situation.Trim()}");
        }

        user.AppendLine();
        user.AppendLine($"Chapter {chapter.Number} text:");
        user.AppendLine(chapter.ContentOriginal);
        user.AppendLine();
        user.AppendLine("Report: continues (true/false); score 1-5 where 5 = flows seamlessly from the "
            + "established story and 1 = reads as a brand-new story; restartSignals (phrases that "
            + "re-introduce known people or places, or reset the premise); contradictions (facts that "
            + "clash with the established story); notes.");
        user.AppendLine("Prefer reporting problems over praise. Do not rewrite anything.");

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }

    public static IReadOnlyList<LlmMessage> BuildContinuityComparison(
        string storySoFar,
        WorldState situationBefore,
        int chapterNumber,
        string textA,
        string textB)
    {
        var system = "You compare two drafts of the same chapter and decide which one better CONTINUES "
            + "the established story. Ignore prose quality, style and length. Reply with ONLY a JSON "
            + "object that matches the required schema.";

        var user = new StringBuilder();
        user.AppendLine($"Two candidate drafts for chapter {chapterNumber} follow. Both are meant to continue the story below.");
        user.AppendLine();
        user.AppendLine("Established story so far:");
        user.AppendLine(string.IsNullOrWhiteSpace(storySoFar) ? "- (this is the first chapter)" : storySoFar);
        user.AppendLine();
        user.AppendLine("Situation before this chapter:");
        AppendField(user, "Time and place", situationBefore.TimeAndPlace);
        if (!string.IsNullOrWhiteSpace(situationBefore.Situation))
        {
            user.AppendLine($"- Situation: {situationBefore.Situation.Trim()}");
        }

        user.AppendLine();
        user.AppendLine("===== CANDIDATE A =====");
        user.AppendLine(textA);
        user.AppendLine();
        user.AppendLine("===== CANDIDATE B =====");
        user.AppendLine(textB);
        user.AppendLine();
        user.AppendLine("Pick the draft that flows from the previous situation, does not restart or "
            + "re-introduce known people or places, and advances unresolved threads. Reply winner "
            + "(A, B or tie) and reasons.");

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }

    public static IReadOnlyList<LlmMessage> BuildChapterSummary(
        Chapter chapter,
        WorldState stateBefore,
        IReadOnlyList<KnowledgeEntry> knowledge)
    {
        var system = "Role: You maintain the story bible for one finished chapter of an ongoing book.\n"
            + "Objective: Record what happened, the situation it leaves behind, and the changes to the knowledge base.\n"
            + "Constraints: Work in English only. Base everything strictly on the chapter text. Never invent facts.\n"
            + "Output: Only a JSON object that matches the required schema.";

        var user = new StringBuilder();
        user.AppendLine($"Summarize chapter {chapter.Number}"
            + (string.IsNullOrWhiteSpace(chapter.Title) ? string.Empty : $" (\"{chapter.Title.Trim()}\")")
            + ".");
        user.AppendLine();
        user.AppendLine("World state before this chapter:");
        AppendField(user, "Time and place", stateBefore.TimeAndPlace);
        if (!string.IsNullOrWhiteSpace(stateBefore.Situation))
        {
            user.AppendLine($"- Situation: {stateBefore.Situation.Trim()}");
        }

        var openThreads = knowledge
            .Where(entry => entry.Kind == KnowledgeKind.Thread && entry.Status == KnowledgeStatus.Open)
            .ToList();
        if (openThreads.Count > 0)
        {
            user.AppendLine();
            user.AppendLine("Open threads — update these by entryId; do NOT create a duplicate thread:");
            foreach (var thread in openThreads)
            {
                user.AppendLine($"- {thread.Title.Trim()} (entryId: {thread.Id})");
            }
        }

        if (knowledge.Count > 0)
        {
            user.AppendLine();
            user.AppendLine("Current knowledge base (update/delete by entry id; keep every detail the chapter does not contradict):");
            const int perEntry = 2000;
            const int total = 16000;
            var used = 0;
            foreach (var entry in knowledge)
            {
                var tags = entry.Tags.Count > 0 ? $" [{string.Join(", ", entry.Tags)}]" : string.Empty;
                user.AppendLine($"- [{entry.Kind}] {entry.Title}{tags} (entryId: {entry.Id})");
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
        user.AppendLine("Reply with:");
        user.AppendLine("- logline: 1-2 sentences on what actually happened in this chapter.");
        user.AppendLine("- timeAndPlace: a short when/where line for the situation immediately after this chapter.");
        user.AppendLine("- situation: the situation after this chapter, in this order: where we are; what changed; what is still unresolved; what follows from this. Do not say \"this sets up next\" and never name chapters. Always write it.");
        user.AppendLine("- knowledgeChanges: the entries this chapter changed.");
        user.AppendLine("- continuityNotes: check the chapter against the world, the initial state and the "
            + "knowledge base above and list any contradictions (a dead character acting, an age or "
            + "relationship that conflicts with an entry, a fixed world rule broken, a fact stated "
            + "differently from an entry). Severity is Info, Warning or Error; reference the exact entry "
            + "title involved. Return an empty array when the chapter contradicts nothing.");
        user.AppendLine("- directionRewrites: only when this chapter changed the story in a way that makes a later "
            + "chapter's planned direction impossible or false, return that chapter's number and a replacement "
            + "direction (2-4 sentences) consistent with what actually happened. Never rewrite the current or "
            + "past chapters, never rewrite a chapter whose direction still holds, and return an empty array "
            + "otherwise.");
        user.AppendLine("Base everything strictly on the chapter text.");
        user.AppendLine();
        user.AppendLine("knowledgeChanges lists only entries that actually changed: operation (Create, "
            + "Update or Delete), the entryId (required for Update/Delete — use the id shown above), the "
            + "title, kind, tags, the full new content, and a short reason. Leave entryId empty when "
            + "creating a new entry. Return an empty array when nothing changed.");
        user.AppendLine();
        user.AppendLine("Status rules: only kind Thread carries a status. For every non-thread change set "
            + "status to None. For a Thread keep status Open while it is unresolved and use Resolved only "
            + "when this chapter actually resolves it.");
        user.AppendLine();
        user.AppendLine("Threads: if this chapter opens an unresolved question, goal or mystery, create a "
            + "Thread entry (kind Thread, status Open) whose title is the open question. If a similar "
            + "thread is already listed under 'Open threads' above, update THAT entry by its entryId "
            + "instead of creating another. If a question is opened and closed within this same chapter, "
            + "do not create a Thread.");

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

    public static IReadOnlyList<LlmMessage> BuildMetadataTranslation(
        string languageCode,
        string bookName,
        string annotation,
        IReadOnlyList<MetadataChapterTitle> chapterTitles)
    {
        var system = "You translate book metadata for a work of fiction. Translate the book title, the "
            + "annotation (blurb) and every chapter title into the target language. Preserve proper names, "
            + "tone and meaning; translate naturally instead of transliterating, and keep the titles short. "
            + "Reply with ONLY a JSON object that matches the required schema.";

        var user = new StringBuilder();
        user.AppendLine($"Translate the following into the language with the ISO code \"{languageCode.Trim()}\".");
        user.AppendLine();
        user.AppendLine($"Book title: {bookName}");

        if (!string.IsNullOrWhiteSpace(annotation))
        {
            user.AppendLine();
            user.AppendLine("Annotation:");
            user.AppendLine(annotation.Trim());
        }

        if (chapterTitles.Count > 0)
        {
            user.AppendLine();
            user.AppendLine("Chapter titles:");
            foreach (var chapter in chapterTitles)
            {
                user.AppendLine($"- {chapter.Number}: {chapter.Title}");
            }
        }

        user.AppendLine();
        user.AppendLine("Return the translated book title as \"name\", the translated annotation as "
            + "\"annotation\", and the translated chapter titles as \"chapterTitles\" (one entry per number).");

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }

    public static string EditorSystem() =>
        "Role: You are a meticulous fiction editor.\n"
        + "Objective: Revise the chapter draft for continuity, pacing, repetition, clarity and style.\n"
        + "Constraints: Preserve the author's voice, the point of view, the tense and the established "
        + "facts. Keep the draft's full length and detail — never summarize or shorten it. Write in "
        + "English only. Never mention chapter numbers or the book itself.\n"
        + "Output: Only the revised chapter prose — no title, headings or commentary.\n"
        + "Do not revise until you are asked to.";

    public static string EditorGather() =>
        "Consult the project with the tools to check continuity and the established facts (characters, "
        + "knowledge, world state, recent loglines) before revising. When you have what you need, reply "
        + "with one short line; do not revise yet.";

    public static IReadOnlyList<LlmMessage> BuildEditorSeed(
        Chapter chapter,
        WorldState stateBefore,
        Project project,
        int recentLoglineCount = 5)
    {
        var user = new StringBuilder();
        user.AppendLine(WriterBrief(chapter));

        var frame = WriterWorldStyle(project.World);
        if (frame.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(frame);
        }

        user.AppendLine();
        user.AppendLine(WriterPosition(project, chapter));

        var state = WriterState(stateBefore);
        if (state.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(state);
        }

        var story = WriterRecap(project, chapter, recentLoglineCount);
        if (story.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(story);
        }

        var threads = OpenThreads(project);
        if (threads.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(threads);
        }

        var lore = WriterWorldLore(project.World);
        if (lore.Length > 0)
        {
            user.AppendLine();
            user.AppendLine(lore);
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
            + "English, with no notes or commentary. Do not begin with the chapter title or a heading line; "
            + "start directly with the prose.");
        user.AppendLine();
        user.AppendLine("Draft to revise:");
        user.AppendLine(draft);
        user.AppendLine();
        user.AppendLine("Return the full revised chapter text.");
        return [LlmMessage.User(user.ToString())];
    }

    public static IReadOnlyList<LlmMessage> BuildEditorRepair(
        string text,
        IReadOnlyList<string> violations,
        bool tenseDrift = false,
        string declaredTense = "")
    {
        var user = new StringBuilder();
        user.AppendLine("The chapter below breaks the style contract. Fix ONLY the listed problems in place; "
            + "keep every story beat, the voice, the point of view and the full length. Do not add or remove "
            + "events.");
        foreach (var violation in violations)
        {
            user.AppendLine($"- Remove this meta reference from the prose: \"{violation}\".");
        }

        if (tenseDrift)
        {
            user.AppendLine($"- The narration drifts away from the declared tense (\"{declaredTense}\"). Rewrite the whole chapter in that tense.");
        }

        user.AppendLine();
        user.AppendLine("Chapter:");
        user.AppendLine(text);
        user.AppendLine();
        user.AppendLine("Output only the corrected chapter text.");
        return [LlmMessage.System(EditorSystem()), LlmMessage.User(user.ToString())];
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
