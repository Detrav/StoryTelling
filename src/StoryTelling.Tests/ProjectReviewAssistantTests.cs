using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Review;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class ProjectReviewAssistantTests
{
    [Fact]
    public async Task ReviewAsync_RunsSinglePassAndParsesFindings()
    {
        const string json = """{"findings":[{"severity":"Warning","area":"Knowledge","title":"Tone vs rating","detail":"Grim tone with a G rating.","suggestion":"Raise the rating."}]}""";
        var client = new ScriptedLlmClient(json, []);
        var assistant = new ProjectReviewAssistant(client, new FakeSettingsService
        {
            Settings = new AppSettings { Model = "m", MaxToolCalls = 5 },
        });

        var findings = await assistant.ReviewAsync(new Project { Name = "Book" }, "check names", ReviewChecks.Facts);

        var finding = Assert.Single(findings);
        Assert.Equal(ReviewSeverity.Warning, finding.Severity);
        Assert.Equal(ReviewArea.Knowledge, finding.Area);
        Assert.Equal("Tone vs rating", finding.Title);
        Assert.Equal("Raise the rating.", finding.Suggestion);
        Assert.Single(client.JsonRequests);
    }

    [Fact]
    public async Task ReviewAsync_EmptyFindings_ReturnsEmpty()
    {
        var assistant = new ProjectReviewAssistant(new ScriptedLlmClient("""{"findings":[]}""", []), new FakeSettingsService());

        var findings = await assistant.ReviewAsync(new Project(), string.Empty, ReviewChecks.Facts);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task ReviewAsync_ParsesReferenceAndFixDroppingInvalidEdits()
    {
        const string json = """{"findings":[{"severity":"Warning","area":"Knowledge","title":"Age gap","detail":"Aria is 200 but looks 20.","suggestion":"Align the age.","reference":"Aria","fix":{"edits":[{"target":"Knowledge","reference":"Aria","field":"Content","value":"Aria is 200 but looks 20."},{"target":"Knowledge","reference":"Aria","field":"Bogus","value":"x"}]}}]}""";
        var assistant = new ProjectReviewAssistant(new ScriptedLlmClient(json, []), new FakeSettingsService());

        var findings = await assistant.ReviewAsync(new Project(), string.Empty, ReviewChecks.Facts);

        var finding = Assert.Single(findings);
        Assert.Equal("Aria", finding.Reference);
        Assert.NotNull(finding.Fix);
        var edit = Assert.Single(finding.Fix!.Edits);
        Assert.Equal(GenerationTarget.Knowledge, edit.Target);
        Assert.Equal("Aria", edit.Reference);
        Assert.Equal("Content", edit.Field);
        Assert.Equal("Aria is 200 but looks 20.", edit.Value);
    }

    [Fact]
    public async Task ReviewAsync_MalformedJson_ReturnsEmpty()
    {
        var assistant = new ProjectReviewAssistant(new ScriptedLlmClient("{ not json", []), new FakeSettingsService());

        var findings = await assistant.ReviewAsync(new Project(), string.Empty, ReviewChecks.Facts);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task ReviewAsync_DropsNoOpFixes()
    {
        const string json = """{"findings":[{"severity":"Warning","area":"Knowledge","title":"Age gap","detail":"Aria is 200 but looks 20.","suggestion":"Align the age.","reference":"Aria","fix":{"edits":[{"target":"Knowledge","reference":"Aria","field":"Content","value":"Aria is 200 but looks 20."},{"target":"Knowledge","reference":"Aria","field":"Tags","value":"mystic"}]}}]}""";
        var assistant = new ProjectReviewAssistant(new ScriptedLlmClient(json, []), new FakeSettingsService());
        var project = new Project
        {
            Knowledge = [new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Aria", Tags = ["mystic"], Content = "Aria is 200 but looks 20." }],
        };

        var findings = await assistant.ReviewAsync(project, string.Empty, ReviewChecks.Facts);

        var finding = Assert.Single(findings);
        Assert.Null(finding.Fix);
    }

    [Fact]
    public async Task ReviewAsync_RunsMultiplePassesAndMergesFindings()
    {
        const string json = """{"reconciliation":[],"findings":[{"severity":"Warning","area":"Knowledge","title":"Tone","detail":"d","suggestion":"","reference":"A"}]}""";
        var client = new ScriptedLlmClient(json, []);
        var assistant = new ProjectReviewAssistant(client, new FakeSettingsService
        {
            Settings = new AppSettings { Model = "m", MaxToolCalls = 0 },
        });

        var findings = await assistant.ReviewAsync(new Project(), string.Empty, ReviewChecks.Facts);

        Assert.Single(findings);
        Assert.Single(client.JsonRequests);
    }

    [Fact]
    public async Task ReviewAsync_TruncatedJson_SalvagesCompleteFindings()
    {
        const string json = "{\"findings\":[{\"severity\":\"Warning\",\"area\":\"Knowledge\",\"title\":\"A\",\"detail\":\"first\"},{\"severity\":\"Error\",\"area\":\"Knowledge\",\"title\":\"B\",\"detail\":\"unterminated";
        var assistant = new ProjectReviewAssistant(new ScriptedLlmClient(json, []), new FakeSettingsService());

        var findings = await assistant.ReviewAsync(new Project(), string.Empty, ReviewChecks.Facts);

        var finding = Assert.Single(findings);
        Assert.Equal("A", finding.Title);
    }

    [Fact]
    public async Task ReviewAsync_DedupesFindingsByAreaReferenceAndTitle()
    {
        const string json = """{"findings":[{"severity":"Error","area":"Knowledge","title":"Two protagonists","detail":"First wording.","suggestion":"","reference":"Elias"},{"severity":"Error","area":"Knowledge","title":"Two protagonists","detail":"Second wording.","suggestion":"","reference":"Elias"}]}""";
        var assistant = new ProjectReviewAssistant(new ScriptedLlmClient(json, []), new FakeSettingsService());

        var findings = await assistant.ReviewAsync(new Project(), string.Empty, ReviewChecks.Facts);

        Assert.Single(findings);
    }
}
