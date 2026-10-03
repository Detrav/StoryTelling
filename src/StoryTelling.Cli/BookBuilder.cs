using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Cli;

internal sealed class BookBuilder
{
    private readonly IGenerationAssistant _assistant;
    private readonly IChapterRunner _runner;
    private readonly IClock _clock;

    public BookBuilder(IGenerationAssistant assistant, IChapterRunner runner, IClock clock)
    {
        _assistant = assistant;
        _runner = runner;
        _clock = clock;
    }

    public async Task<Project> CreateSetupAsync(string brief, int characters, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var project = new Project
        {
            Name = "Untitled",
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        await GenerateIntoAsync(project, GenerationTarget.ProjectName, brief, 1, cancellationToken);
        await GenerateIntoAsync(project, GenerationTarget.World, brief, 1, cancellationToken);

        if (characters > 0)
        {
            var castBrief = string.IsNullOrWhiteSpace(brief)
                ? $"Create {characters} distinct central characters."
                : $"{brief.Trim()} Focus on the main cast: create {characters} distinct central characters.";
            await GenerateIntoAsync(project, GenerationTarget.Knowledge, castBrief, characters, cancellationToken, KnowledgeKind.Character);
        }

        await GenerateIntoAsync(project, GenerationTarget.InitialWorldState, brief, 1, cancellationToken);
        project.UpdatedUtc = _clock.UtcNow;
        return project;
    }

    public async Task WriteChaptersAsync(Project project, int count, CancellationToken cancellationToken)
    {
        for (var i = 0; i < count; i++)
        {
            var number = project.Chapters.Count + 1;
            Console.WriteLine($"  Writing chapter {number}…");
            var result = await _runner.GenerateNextAsync(project, new ConsoleProgress($"chapter {number}"), cancellationToken);
            Console.WriteLine($"  Chapter {number}: {result.Text.Length} chars, {result.ToolCalls} tool calls");
            Console.WriteLine($"    logline: {result.Logline}");
        }

        project.UpdatedUtc = _clock.UtcNow;
    }

    public async Task<int> GenerateIntoAsync(
        Project project,
        GenerationTarget target,
        string brief,
        int variants,
        CancellationToken cancellationToken,
        KnowledgeKind? kindOverride = null)
    {
        var request = new GenerationRequest
        {
            Target = target,
            Brief = brief,
            Variants = variants,
            Context = BuildContext(project),
            Snapshot = project,
            Avoid = target == GenerationTarget.Knowledge
                ? [.. project.Knowledge.Select(entry => entry.Title.Trim()).Where(title => title.Length > 0)]
                : [],
        };

        var options = await _assistant.GenerateAsync(request, null, new ConsoleProgress(target.ToString()), cancellationToken);
        var applied = options.Count(option => Apply(project, target, option, kindOverride));
        Console.WriteLine($"  {target}: {options.Count} option(s), applied {applied}");
        return applied;
    }

    internal static GenerationContext BuildContext(Project project) => new()
    {
        Fields = new Dictionary<string, string>
        {
            ["ProjectName"] = project.Name,
            ["WorldTitle"] = project.World.Title,
            ["WorldBody"] = project.World.Body,
            ["Genre"] = project.World.Genre,
            ["Tone"] = project.World.Tone,
            ["Style"] = project.World.Style,
            ["PointOfView"] = project.World.PointOfView,
            ["Tense"] = project.World.Tense,
            ["Rating"] = project.World.Rating,
        },
    };

    private static bool Apply(Project project, GenerationTarget target, GenerationOption option, KnowledgeKind? kindOverride)
    {
        var fields = option.Fields;

        switch (target)
        {
            case GenerationTarget.ProjectName:
                project.Name = Get(fields, "ProjectName") ?? project.Name;
                return true;

            case GenerationTarget.World:
                project.World.Title = Get(fields, "WorldTitle") ?? project.World.Title;
                project.World.Body = Get(fields, "WorldBody") ?? project.World.Body;
                project.World.Genre = Get(fields, "Genre") ?? project.World.Genre;
                project.World.Tone = Get(fields, "Tone") ?? project.World.Tone;
                project.World.Style = Get(fields, "Style") ?? project.World.Style;
                project.World.PointOfView = Get(fields, "PointOfView") ?? project.World.PointOfView;
                project.World.Tense = Get(fields, "Tense") ?? project.World.Tense;
                project.World.Rating = Get(fields, "Rating") ?? project.World.Rating;
                return true;

            case GenerationTarget.InitialWorldState:
                project.InitialWorldState.TimeAndPlace = Get(fields, "TimeAndPlace") ?? project.InitialWorldState.TimeAndPlace;
                project.InitialWorldState.Situation = Get(fields, "Situation") ?? project.InitialWorldState.Situation;
                return true;

            case GenerationTarget.Knowledge:
                var title = Get(fields, "Title");
                if (title is null)
                {
                    return false;
                }

                project.Knowledge.Add(new KnowledgeEntry
                {
                    Id = Guid.NewGuid(),
                    Kind = kindOverride ?? ParseKind(Get(fields, "Kind")),
                    Title = title,
                    Tags = Split(Get(fields, "Tags")),
                    Content = Get(fields, "Content") ?? string.Empty,
                });
                return true;

            default:
                return false;
        }
    }

    private static KnowledgeKind ParseKind(string? value) =>
        Enum.TryParse<KnowledgeKind>(value, ignoreCase: true, out var kind) ? kind : KnowledgeKind.Note;

    private static string? Get(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static List<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : [.. value.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
