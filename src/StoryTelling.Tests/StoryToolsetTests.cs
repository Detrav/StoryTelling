using System.Text.Json.Nodes;
using StoryTelling.Application.Story;
using StoryTelling.Application.Tools;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class StoryToolsetTests
{
    [Fact]
    public void Definitions_ExposeTheExpectedTools()
    {
        var names = new StoryToolset(new StoryQuery(Project())).Definitions.Select(definition => definition.Name).ToHashSet();

        Assert.Equal(
            new HashSet<string>
            {
                "story", "characters", "character", "initial_world_state",
                "recent_loglines", "list_entries", "get_entry", "search_knowledge",
            },
            names);
    }

    [Fact]
    public void Invoke_Characters_ListsCast()
    {
        var result = new StoryToolset(new StoryQuery(Project())).Invoke("characters", "{}");

        Assert.Contains("Aria (protagonist)", result);
        Assert.Contains("Bran (smith)", result);
    }

    [Fact]
    public void Invoke_Character_FormatsProfile()
    {
        var result = new StoryToolset(new StoryQuery(Project())).Invoke("character", """{"name":"Aria"}""");

        Assert.Contains("Title: Aria", result);
        Assert.Contains("Kind: Character", result);
        Assert.Contains("A frontier scout.", result);
    }

    [Fact]
    public void Invoke_SearchKnowledge_UsesArguments()
    {
        var result = new StoryToolset(new StoryQuery(Project())).Invoke("search_knowledge", """{"query":"frozen frontier","topK":1}""");

        Assert.Contains("Ashen Reach", result);
    }

    [Fact]
    public void Invoke_MissingRequiredArgument_Throws()
    {
        var toolset = new StoryToolset(new StoryQuery(Project()));

        Assert.Throws<InvalidOperationException>(() => toolset.Invoke("character", "{}"));
    }

    [Fact]
    public void Invoke_UnknownTool_Throws()
    {
        var toolset = new StoryToolset(new StoryQuery(Project()));

        Assert.Throws<InvalidOperationException>(() => toolset.Invoke("does_not_exist", "{}"));
    }

    [Fact]
    public void Definitions_HaveValidParameterSchemas()
    {
        var toolset = new StoryToolset(new StoryQuery(Project()));

        Assert.All(toolset.Definitions, definition =>
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Description));
            Assert.NotNull(definition.Parameters);
            Assert.Equal("object", definition.Parameters["type"]!.GetValue<string>());
        });
    }

    private static Project Project() => new()
    {
        Name = "The Ember Crown",
        World = new World { Genre = "dark fantasy" },
        Knowledge =
        [
            new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Aria", Tags = ["protagonist"], Content = "A frontier scout." },
            new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Bran", Tags = ["smith"] },
            new KnowledgeEntry { Kind = KnowledgeKind.Place, Title = "Ashen Reach", Content = "A frozen frontier of ash." },
        ],
    };
}
