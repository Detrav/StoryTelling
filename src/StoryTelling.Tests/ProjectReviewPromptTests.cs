using StoryTelling.Application.Prompts;
using StoryTelling.Application.Review;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class ProjectReviewPromptTests
{
    private static Project Sample() => new()
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
        InitialWorldState = new WorldState { TimeAndPlace = "2:17 AM, Undercity", Situation = "A body in the corridor." },
        Knowledge =
        [
            new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Elias Thorne", Tags = ["protagonist"], Content = "A sleuth." },
            new KnowledgeEntry { Kind = KnowledgeKind.Place, Title = "Pervomaysky Textile Factory", Tags = ["crime scene"], Content = "Outskirts of central Moscow." },
        ],
    };

    [Fact]
    public void BuildReview_NumbersCheck_CarriesReconciliationAndFixRules()
    {
        var text = Render(PromptTemplates.BuildReview(Sample(), string.Empty, ReviewChecks.Numbers));

        Assert.Contains("Reconcile the numbers", text);
        Assert.Contains("at least 12 years older than their child", text);
        Assert.Contains("reconciliation", text);
        Assert.Contains("MUST differ from the current one", text);
        Assert.Contains("Rename an entry", text);
        Assert.Contains("Merge duplicates", text);
        Assert.Contains("Elias Thorne", text);
        Assert.Contains("2:17 AM, Undercity", text);
    }

    [Fact]
    public void BuildReview_SettingCheck_IncludesWorldAndPlacesOnly()
    {
        var text = Render(PromptTemplates.BuildReview(Sample(), string.Empty, ReviewChecks.Setting));

        Assert.Contains("The Glass Undercity of Veridian", text);
        Assert.Contains("Pervomaysky Textile Factory", text);
        Assert.DoesNotContain("Elias Thorne", text);
        Assert.Contains("Setting vs entries", text);
    }

    [Fact]
    public void BuildReview_IdentityCheck_OnlyIncludesCharacters()
    {
        var text = Render(PromptTemplates.BuildReview(Sample(), string.Empty, ReviewChecks.Identity));

        Assert.Contains("Elias Thorne", text);
        Assert.DoesNotContain("Pervomaysky Textile Factory", text);
        Assert.Contains("Identity mix-ups", text);
    }

    private static string Render(IReadOnlyList<Application.Llm.LlmMessage> messages) =>
        string.Join("\n", messages.Select(message => message.Content));
}
