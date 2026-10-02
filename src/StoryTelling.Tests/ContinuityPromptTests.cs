using StoryTelling.Application.Prompts;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class ContinuityPromptTests
{
    [Fact]
    public void BuildContinuityReview_IncludesWorldInitialStateAndProse()
    {
        var project = new Project
        {
            World = new World { Title = "Verdant", Body = "A tidally locked ocean world with perpetual dusk." },
            InitialWorldState = new WorldState { TimeAndPlace = "The reef, night", Situation = "Elara keeps the beacon." },
        };
        var chapter = new Chapter
        {
            Number = 1,
            Direction = "The signal answers.",
            ContentOriginal = "The sun bleeds over the ice and dusk falls.",
        };

        var messages = PromptTemplates.BuildContinuityReview(project, chapter, []);
        var user = messages[1].Content;

        Assert.Contains("perpetual dusk", user);
        Assert.Contains("Elara keeps the beacon", user);
        Assert.Contains("The sun bleeds over the ice", user);
        Assert.Contains("The signal answers", user);
    }

    [Fact]
    public void BuildContinuityReview_ListsOpenThreads()
    {
        var project = new Project();
        var chapter = new Chapter { Number = 1 };

        var messages = PromptTemplates.BuildContinuityReview(project, chapter,
        [
            new KnowledgeEntry { Kind = KnowledgeKind.Thread, Title = "Who is the voice?", Status = KnowledgeStatus.Open, Content = "open" },
            new KnowledgeEntry { Kind = KnowledgeKind.Thread, Title = "The old debt", Status = KnowledgeStatus.Resolved, Content = "paid" },
        ]);
        var user = messages[1].Content;

        var heading = user.IndexOf("Open threads that should be tracked", StringComparison.Ordinal);
        Assert.True(heading >= 0);
        Assert.Contains("Who is the voice?", user[heading..]);
    }
}
