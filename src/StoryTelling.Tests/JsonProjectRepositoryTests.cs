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

        Assert.Contains($"\"schemaVersion\": {ProjectSchema.Version}", json);
        Assert.Contains("\"world\"", json);
        Assert.Contains("\"initialWorldState\"", json);
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

        await Assert.ThrowsAnyAsync<JsonException>(() => repository.LoadAsync(path));
    }

    [Fact]
    public async Task Load_MissingFile_Throws()
    {
        var repository = new JsonProjectRepository();
        var path = Path.Combine(_directory, "does-not-exist.story.json");

        await Assert.ThrowsAsync<FileNotFoundException>(() => repository.LoadAsync(path));
    }

    [Fact]
    public async Task Load_MigratesVersion1Project()
    {
        var repository = new JsonProjectRepository();
        var path = Path.Combine(_directory, "legacy.story.json");
        await File.WriteAllTextAsync(path, """
        {
          "schemaVersion": 1,
          "name": "Legacy",
          "worldState": { "timeAndPlace": "Dawn", "description": "Old state." },
          "lore": { "title": "Ashen Reach", "body": "A dying empire.", "tags": ["fantasy"] },
          "frame": { "genre": "fantasy", "tone": "grim", "premise": "A rebellion.", "direction": "Rise." },
          "characters": [
            { "name": "Aria", "role": "protagonist", "description": "A scout.", "traits": ["brave"] }
          ],
          "knowledge": []
        }
        """);

        var project = await repository.LoadAsync(path);

        Assert.Equal(ProjectSchema.Version, project.SchemaVersion);
        Assert.Equal("Ashen Reach", project.World.Title);
        Assert.Equal("A dying empire.", project.World.Body);
        Assert.Equal("fantasy", project.World.Genre);
        Assert.Equal("grim", project.World.Tone);
        Assert.Equal("Dawn", project.InitialWorldState.TimeAndPlace);
        var character = Assert.Single(project.Knowledge);
        Assert.Equal(KnowledgeKind.Character, character.Kind);
        Assert.Equal("Aria", character.Title);
        Assert.Contains("brave", character.Tags);
        Assert.Contains("A scout.", character.Content);
    }

    [Fact]
    public async Task Load_MigratesVersion2AndDropsChapterSummary()
    {
        var repository = new JsonProjectRepository();
        var path = Path.Combine(_directory, "v2.story.json");
        await File.WriteAllTextAsync(path, """
        {
          "schemaVersion": 2,
          "name": "Legacy v2",
          "chapters": [
            { "number": 1, "title": "One", "contentOriginal": "Text.", "summary": "Old recap.", "logline": "A logline." }
          ]
        }
        """);

        var project = await repository.LoadAsync(path);

        Assert.Equal(ProjectSchema.Version, project.SchemaVersion);
        var chapter = Assert.Single(project.Chapters);
        Assert.Equal("Text.", chapter.ContentOriginal);
        Assert.Equal("A logline.", chapter.Logline);
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
        World = new World
        {
            Title = "Ashen Reach",
            Body = "A dying empire under a pale sun.",
            Tags = ["fantasy"],
            Genre = "fantasy",
            Tone = "grim",
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
                Logline = "A scout flees a burning keep.",
                Status = ChapterStatus.Generated,
                CreatedUtc = DateTimeOffset.UnixEpoch,
            },
        ],
        InitialWorldState = new WorldState
        {
            TimeAndPlace = "Dusk, the cliffs above the keep",
            Description = "Aria crouches in the ruins of the keep with the relic.",
        },
    };
}
