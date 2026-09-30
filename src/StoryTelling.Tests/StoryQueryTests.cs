using StoryTelling.Application.Story;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class StoryQueryTests
{
    [Fact]
    public void Story_ReturnsFrameAndLore()
    {
        var story = new StoryQuery(Project()).Story();

        Assert.Equal("The Ember Crown", story.Name);
        Assert.Equal("dark fantasy", story.Genre);
        Assert.Equal("Ashen Reach", story.LoreTitle);
        Assert.Equal("A frozen frontier.", story.LoreBody);
    }

    [Fact]
    public void Characters_ListsCast()
    {
        var characters = new StoryQuery(Project()).Characters();

        Assert.Equal(2, characters.Count);
        Assert.Contains(characters, character => character.Name == "Aria" && character.Role == "protagonist");
    }

    [Fact]
    public void Character_IsCaseInsensitive()
    {
        var character = new StoryQuery(Project()).Character("aria");

        Assert.NotNull(character);
        Assert.Equal("Aria", character!.Name);
    }

    [Fact]
    public void WorldState_ReturnsCurrentState()
    {
        var state = new StoryQuery(Project()).WorldState();

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
        Frame = new StoryFrame { Genre = "dark fantasy", Tone = "grim" },
        Lore = new WorldLore { Title = "Ashen Reach", Body = "A frozen frontier." },
        Characters =
        [
            new Character { Name = "Aria", Role = "protagonist" },
            new Character { Name = "Bran", Role = "smith" },
        ],
        WorldState = new WorldState { TimeAndPlace = "Dusk above the keep", Description = "Aria crouches in the ruins." },
        Knowledge =
        [
            new KnowledgeEntry { Kind = KnowledgeKind.Place, Title = "Ashen Reach", Content = "A frozen frontier of ash.", Tags = ["region"] },
        ],
        Chapters =
        [
            new Chapter { Number = 1, Title = "Embers", Logline = "A scout flees." },
            new Chapter { Number = 2, Title = "Ash", Logline = "The keep falls." },
        ],
    };
}
