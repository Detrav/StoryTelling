using StoryTelling.Domain;
using StoryTelling.Infrastructure;
using StoryTelling.Infrastructure.Json;

namespace StoryTelling.Tests;

public sealed class JsonProjectRepositoryTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "StoryTellingTests", Guid.NewGuid().ToString("N"));

    public JsonProjectRepositoryTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsProject()
    {
        var repository = new JsonProjectRepository();
        var path = Path.Combine(_directory, "the-ember-crown.story.json");
        var original = SampleProject();

        await repository.SaveAsync(original, path);
        var loaded = await repository.LoadAsync(path);

        Assert.Equal(StoryJson.Serialize(original), StoryJson.Serialize(loaded));
        Assert.Equal(original.Id, loaded.Id);
        Assert.Single(loaded.Chapters);
        Assert.Equal("Дым поднимался.", loaded.Chapters[0].ContentTranslated);
    }

    [Fact]
    public async Task Save_WritesRelaxedUtf8AndCamelCaseShape()
    {
        var repository = new JsonProjectRepository();
        var path = Path.Combine(_directory, "shape.story.json");

        await repository.SaveAsync(SampleProject(), path);
        var json = await File.ReadAllTextAsync(path);

        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"worldState\"", json);
        Assert.Contains("\"timeAndPlace\"", json);
        Assert.Contains("\"activeThreads\"", json);
        Assert.Contains("\"Generated\"", json);
        Assert.Contains("Дым поднимался.", json);
        Assert.DoesNotContain(Directory.GetFiles(_directory), file => file.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Load_RejectsNewerSchemaVersion()
    {
        var repository = new JsonProjectRepository();
        var path = Path.Combine(_directory, "future.story.json");
        var json = $$"""
        {
          "schemaVersion": {{ProjectSchema.Version + 1}},
          "id": "{{Guid.NewGuid()}}",
          "name": "From the future"
        }
        """;
        await File.WriteAllTextAsync(path, json);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => repository.LoadAsync(path));

        Assert.Contains("newer", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static Project SampleProject() => new()
    {
        Name = "The Ember Crown",
        CreatedUtc = DateTimeOffset.UnixEpoch,
        UpdatedUtc = DateTimeOffset.UnixEpoch.AddHours(2),
        Settings = new StorySettings { TargetLanguage = "ru" },
        Lore = new WorldLore
        {
            Title = "Ashen Reach",
            Body = "A dying empire under a pale sun.",
            Tags = ["fantasy"],
        },
        Characters =
        [
            new Character
            {
                Name = "Aria",
                Description = "A frontier scout.",
                Traits = ["brave", "wry"],
                Goals = "Find her missing brother.",
            },
        ],
        Plot = new PlotDescription
        {
            Genre = "fantasy",
            Tone = "grim",
            Premise = "A rebellion against the ember throne.",
            Direction = "Rise, fracture, resolve.",
            ChapterCount = 3,
        },
        ExtraFiles = [new ExtraFile { Name = "bestiary.md", Content = "Wyverns nest in cliffs.", Tags = ["lore"] }],
        Outline = [new OutlineEntry { ChapterNumber = 1, Title = "Embers", Direction = "Introduce Aria.", Approved = true }],
        Chapters =
        [
            new Chapter
            {
                Number = 1,
                Title = "Embers",
                Direction = "Introduce Aria.",
                ContentOriginal = "The smoke rose.",
                ContentTranslated = "Дым поднимался.",
                Summary = "Aria escapes the burning keep.",
                Status = ChapterStatus.Generated,
                CreatedUtc = DateTimeOffset.UnixEpoch,
            },
        ],
        WorldState = new WorldState
        {
            TimeAndPlace = "Dusk, the cliffs above the keep",
            Characters =
            [
                new CharacterState
                {
                    Name = "Aria",
                    Status = "unhurt",
                    Location = "cliffs",
                    Goals = "escape",
                    Knowledge = "the relic is real",
                    Relationships = "owes Bran",
                },
            ],
            Locations = ["the cliffs"],
            Items = ["the relic"],
            ActiveThreads = ["escape the crown's hunters"],
            ResolvedThreads = ["the keep burns"],
            RecentEvents = ["the keep fell at dusk"],
            OpenQuestions = ["what is the relic?"],
        },
        Transcript = [new AssistantMessage { Role = AssistantRole.User, Content = "Plan the arc.", CreatedUtc = DateTimeOffset.UnixEpoch }],
    };
}
