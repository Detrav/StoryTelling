using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Cli;

internal sealed class BookBuilder
{
    private readonly IGenerationAssistant _assistant;
    private readonly IChapterAgent _chapterAgent;
    private readonly IClock _clock;

    public BookBuilder(IGenerationAssistant assistant, IChapterAgent chapterAgent, IClock clock)
    {
        _assistant = assistant;
        _chapterAgent = chapterAgent;
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
        await GenerateIntoAsync(project, GenerationTarget.Frame, brief, 1, cancellationToken);

        if (characters > 0)
        {
            await GenerateIntoAsync(project, GenerationTarget.Character, brief, characters, cancellationToken);
        }

        await GenerateIntoAsync(project, GenerationTarget.WorldState, brief, 1, cancellationToken);
        project.UpdatedUtc = _clock.UtcNow;
        return project;
    }

    public async Task WriteChaptersAsync(Project project, int count, CancellationToken cancellationToken)
    {
        for (var i = 0; i < count; i++)
        {
            var number = project.Chapters.Count + 1;
            var stateBefore = project.Chapters.Count > 0 ? project.Chapters[^1].WorldState ?? project.WorldState : project.WorldState;
            var chapter = new Chapter
            {
                Number = number,
                Title = $"Chapter {number}",
                Status = ChapterStatus.Draft,
                CreatedUtc = _clock.UtcNow,
            };
            project.Chapters.Add(chapter);

            Console.WriteLine($"  Writing chapter {number}…");
            var context = new WriterContext(project, chapter, stateBefore, ChapterContextAssembler.DefaultTokenBudget);
            var draft = await _chapterAgent.WriteAsync(context, new ConsoleProgress($"chapter {number}"), null, cancellationToken);

            chapter.ContentOriginal = draft.Text;
            chapter.Status = ChapterStatus.Generated;
            project.UpdatedUtc = _clock.UtcNow;
            Console.WriteLine($"  Chapter {number}: {draft.Text.Length} chars, {draft.ToolCalls} tool calls");
        }
    }

    public async Task<int> GenerateIntoAsync(Project project, GenerationTarget target, string brief, int variants, CancellationToken cancellationToken)
    {
        var request = new GenerationRequest
        {
            Target = target,
            Brief = brief,
            Variants = variants,
            Context = BuildContext(project),
            Snapshot = project,
        };

        var options = await _assistant.GenerateAsync(request, null, new ConsoleProgress(target.ToString()), cancellationToken);
        var applied = options.Count(option => Apply(project, target, option));
        Console.WriteLine($"  {target}: {options.Count} option(s), applied {applied}");
        return applied;
    }

    internal static GenerationContext BuildContext(Project project) => new()
    {
        Fields = new Dictionary<string, string>
        {
            ["ProjectName"] = project.Name,
            ["WorldTitle"] = project.Lore.Title,
            ["WorldBody"] = project.Lore.Body,
            ["Genre"] = project.Frame.Genre,
            ["Tone"] = project.Frame.Tone,
            ["Style"] = project.Frame.Style,
            ["PointOfView"] = project.Frame.PointOfView,
            ["Tense"] = project.Frame.Tense,
            ["Rating"] = project.Frame.Rating,
            ["Premise"] = project.Frame.Premise,
            ["Direction"] = project.Frame.Direction,
        },
        Cast = [.. project.Characters
            .Where(character => !string.IsNullOrWhiteSpace(character.Name))
            .Select(character => string.IsNullOrWhiteSpace(character.Role) ? character.Name : $"{character.Name} — {character.Role}")],
    };

    private static bool Apply(Project project, GenerationTarget target, GenerationOption option)
    {
        var fields = option.Fields;

        switch (target)
        {
            case GenerationTarget.ProjectName:
                project.Name = Get(fields, "ProjectName") ?? project.Name;
                return true;

            case GenerationTarget.World:
                project.Lore.Title = Get(fields, "WorldTitle") ?? project.Lore.Title;
                project.Lore.Body = Get(fields, "WorldBody") ?? project.Lore.Body;
                return true;

            case GenerationTarget.Frame:
                project.Frame.Genre = Get(fields, "Genre") ?? project.Frame.Genre;
                project.Frame.Tone = Get(fields, "Tone") ?? project.Frame.Tone;
                project.Frame.Style = Get(fields, "Style") ?? project.Frame.Style;
                project.Frame.PointOfView = Get(fields, "PointOfView") ?? project.Frame.PointOfView;
                project.Frame.Tense = Get(fields, "Tense") ?? project.Frame.Tense;
                project.Frame.Rating = Get(fields, "Rating") ?? project.Frame.Rating;
                project.Frame.Premise = Get(fields, "Premise") ?? project.Frame.Premise;
                project.Frame.Direction = Get(fields, "Direction") ?? project.Frame.Direction;
                return true;

            case GenerationTarget.Premise:
                project.Frame.Premise = Get(fields, "Premise") ?? project.Frame.Premise;
                return true;

            case GenerationTarget.WorldState:
                project.WorldState.TimeAndPlace = Get(fields, "TimeAndPlace") ?? project.WorldState.TimeAndPlace;
                project.WorldState.Description = Get(fields, "Description") ?? project.WorldState.Description;
                return true;

            case GenerationTarget.Character:
                var name = Get(fields, "Name");
                if (name is null)
                {
                    return false;
                }

                project.Characters.Add(new Character
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Role = Get(fields, "Role") ?? string.Empty,
                    Age = Get(fields, "Age") ?? string.Empty,
                    Description = Get(fields, "Description") ?? string.Empty,
                    Personality = Get(fields, "Personality") ?? string.Empty,
                    Background = Get(fields, "Background") ?? string.Empty,
                    Goals = Get(fields, "Goals") ?? string.Empty,
                    Traits = Split(Get(fields, "Traits")),
                });
                return true;

            default:
                return false;
        }
    }

    private static string? Get(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static List<string> Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : [.. value.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
