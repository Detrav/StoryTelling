using System.Text.Json;
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
        Assert.Equal("Дым поднимался.", loaded.Chapters[0].Translations["ru"]);
        Assert.Equal("bestiary.md", loaded.Knowledge.Single().Title);
        Assert.Equal(KnowledgeKind.Note, loaded.Knowledge.Single().Kind);
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
        Assert.Contains("\"description\"", json);
        Assert.Contains("\"knowledge\"", json);
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

    [Fact]
    public async Task Load_ToleratesMissingFields()
    {
        var repository = new JsonProjectRepository();
        var path = Path.Combine(_directory, "minimal.story.json");
        await File.WriteAllTextAsync(path, $$"""
        {
          "schemaVersion": {{ProjectSchema.Version}},
          "name": "Minimal"
        }
        """);

        var project = await repository.LoadAsync(path);

        Assert.Equal("Minimal", project.Name);
        Assert.Empty(project.Chapters);
        Assert.NotNull(project.Settings);
    }

    [Fact]
    public async Task Load_CorruptJson_Throws()
    {
        var repository = new JsonProjectRepository();
        var path = Path.Combine(_directory, "corrupt.story.json");
        await File.WriteAllTextAsync(path, "{ not valid json");

        await Assert.ThrowsAsync<JsonException>(() => repository.LoadAsync(path));
    }

    [Fact]
    public async Task Load_MissingFile_Throws()
    {
        var repository = new JsonProjectRepository();
        var path = Path.Combine(_directory, "does-not-exist.story.json");

        await Assert.ThrowsAsync<FileNotFoundException>(() => repository.LoadAsync(path));
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
        Settings = new StorySettings { TargetLanguages = ["ru", "de"] },
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
        Frame = new StoryFrame
        {
            Genre = "fantasy",
            Tone = "grim",
            Premise = "A rebellion against the ember throne.",
            Direction = "Rise, fracture, resolve.",
        },
        Knowledge = [new KnowledgeEntry { Kind = KnowledgeKind.Note, Title = "bestiary.md", Content = "Wyverns nest in cliffs.", Tags = ["lore"] }],
        Chapters =
        [
            new Chapter
            {
                Number = 1,
                Title = "Embers",
                Direction = "Introduce Aria.",
                Notes = "Keep it tense.",
                ContentOriginal = "The smoke rose.",
                Translations = new SortedDictionary<string, string> { ["ru"] = "Дым поднимался." },
                Summary = "Aria escapes the burning keep.",
                Logline = "A scout flees a burning keep.",
                Status = ChapterStatus.Generated,
                CreatedUtc = DateTimeOffset.UnixEpoch,
            },
        ],
        WorldState = new WorldState
        {
            TimeAndPlace = "Dusk, the cliffs above the keep",
            Description = "Aria crouches in the ruins of the keep with the relic.",
        },
    };
}
