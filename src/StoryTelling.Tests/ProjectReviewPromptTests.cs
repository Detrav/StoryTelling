using StoryTelling.Application.Prompts;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class ProjectReviewPromptTests
{
    [Fact]
    public void BuildReview_IncludesWorldChecklistAndFixRules()
    {
        var project = new Project
        {
            Name = "Dirty mirrortag",
            World = new World
            {
                Title = "The Glass Undercity of Veridian",
                Body = "A neo-noir mirrorworld beneath a decaying megacity.",
                Genre = "Neo-noir detective thriller",
                PointOfView = "First person",
                Tense = "Present",
            },
            InitialWorldState = new WorldState { TimeAndPlace = "2:17 AM, Undercity" },
            Knowledge =
            [
                new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Elias Thorne", Tags = ["protagonist"], Content = "A sleuth." },
            ],
        };

        var messages = PromptTemplates.BuildReview(project, string.Empty);
        var text = string.Join("\n", messages.Select(message => message.Content));

        Assert.Contains("The Glass Undercity of Veridian", text);
        Assert.Contains("2:17 AM, Undercity", text);
        Assert.Contains("Ages and dates", text);
        Assert.Contains("Timeline", text);
        Assert.Contains("Tags vs content", text);
        Assert.Contains("Setting vs entries", text);
        Assert.Contains("Identity mix-ups", text);
        Assert.Contains("Reconcile the numbers", text);
        Assert.Contains("at most 12 findings", text);
        Assert.Contains("at least 12 years older than their child", text);
        Assert.Contains("reconciliation", text);
        Assert.Contains("differ from the current value", text);
    }
}
