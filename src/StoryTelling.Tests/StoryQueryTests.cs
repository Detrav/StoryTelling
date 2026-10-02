using StoryTelling.Application.Story;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class StoryQueryTests
{
    [Fact]
    public void Story_ReturnsWorldAndFrame()
    {
        var story = new StoryQuery(Project()).Story();

        Assert.Equal("The Ember Crown", story.Name);
        Assert.Equal("dark fantasy", story.Genre);
        Assert.Equal("Ashen Reach", story.WorldTitle);
        Assert.Equal("A frozen frontier.", story.WorldBody);
    }

    [Fact]
    public void Characters_ListsCast()
    {
        var characters = new StoryQuery(Project()).Characters();

        Assert.Equal(2, characters.Count);
        Assert.Contains(characters, character => character.Title == "Aria" && character.Tags.Contains("protagonist"));
    }

    [Fact]
    public void Character_IsCaseInsensitive()
    {
        var character = new StoryQuery(Project()).Character("aria");

        Assert.NotNull(character);
        Assert.Equal("Aria", character!.Title);
    }

    [Fact]
    public void InitialWorldState_ReturnsSeed()
    {
        var state = new StoryQuery(Project()).InitialWorldState();

        Assert.Equal("Dusk above the keep", state.TimeAndPlace);
    }

    [Fact]
    public void RecentLoglines_ReturnsLastN()
    {
        var loglines = new StoryQuery(Project()).RecentLoglines(1);

        var logline = Assert.Single(loglines);
        Assert.Equal(2, logline.Number);
        Assert.Equal("The keep falls.", logline.Logline);
    }

    [Fact]
    public void RecentLoglines_BeforeNumber_ExcludesLaterChapters()
    {
        var project = Project();
        project.Chapters.Add(new Chapter { Number = 3, Title = "Ember", Logline = "A new dawn." });

        var loglines = new StoryQuery(project, beforeNumber: 2).RecentLoglines(3);

        var logline = Assert.Single(loglines);
        Assert.Equal(1, logline.Number);
    }

    [Fact]
    public void ListEntries_FiltersByKind()
    {
        var places = new StoryQuery(Project()).ListEntries(KnowledgeKind.Place);

        var place = Assert.Single(places);
        Assert.Equal("Ashen Reach", place.Title);
    }

    [Fact]
    public void GetEntry_FindsByIdAndTitle()
    {
        var query = new StoryQuery(Project());
        var byTitle = query.GetEntry("Ashen Reach");
        Assert.NotNull(byTitle);

        var byId = query.GetEntry(byTitle!.Id.ToString());
        Assert.NotNull(byId);
        Assert.Equal(byTitle.Id, byId!.Id);
    }

    [Fact]
    public void SearchKnowledge_ReturnsRelevantFragment()
    {
        var fragments = new StoryQuery(Project()).SearchKnowledge("frozen frontier", kind: null, topK: 3);

        Assert.Contains(fragments, fragment => fragment.Title == "Ashen Reach");
    }

    private static Project Project() => new()
    {
        Name = "The Ember Crown",
        World = new World { Genre = "dark fantasy", Tone = "grim", Title = "Ashen Reach", Body = "A frozen frontier." },
        Knowledge =
        [
            new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Aria", Tags = ["protagonist"], Content = "A frontier scout." },
            new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Bran", Tags = ["smith"] },
            new KnowledgeEntry { Kind = KnowledgeKind.Place, Title = "Ashen Reach", Content = "A frozen frontier of ash.", Tags = ["region"] },
        ],
        InitialWorldState = new WorldState { TimeAndPlace = "Dusk above the keep", Situation = "Aria crouches in the ruins." },
        Chapters =
        [
            new Chapter { Number = 1, Title = "Embers", Logline = "A scout flees." },
            new Chapter { Number = 2, Title = "Ash", Logline = "The keep falls." },
        ],
    };
}
