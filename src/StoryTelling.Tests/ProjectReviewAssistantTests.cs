using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Review;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

public sealed class ProjectReviewAssistantTests
{
    [Fact]
    public async Task ReviewAsync_ParsesFindingsAndUsesTools()
    {
        const string json = """{"findings":[{"severity":"Warning","area":"Frame","title":"Tone vs rating","detail":"Grim tone with a G rating.","suggestion":"Raise the rating."}]}""";
        var client = new ScriptedLlmClient(json,
        [
            new LlmToolResponse(string.Empty, "tool_calls", [new LlmToolCall("c1", "story", "{}")]),
            new LlmToolResponse(string.Empty, "stop", []),
        ]);
        var assistant = new ProjectReviewAssistant(client, new FakeSettingsService
        {
            Settings = new AppSettings { Model = "m", MaxToolCalls = 5 },
        });

        var findings = await assistant.ReviewAsync(new Project { Name = "Book" }, "check names");

        var finding = Assert.Single(findings);
        Assert.Equal(ReviewSeverity.Warning, finding.Severity);
        Assert.Equal(ReviewArea.Frame, finding.Area);
        Assert.Equal("Tone vs rating", finding.Title);
        Assert.Equal("Raise the rating.", finding.Suggestion);
        Assert.NotNull(client.LastJsonRequest);
        Assert.Contains(client.LastJsonRequest!.Messages, message => message.Role == LlmRole.Tool);
    }

    [Fact]
    public async Task ReviewAsync_EmptyFindings_ReturnsEmpty()
    {
        var assistant = new ProjectReviewAssistant(new ScriptedLlmClient("""{"findings":[]}""", []), new FakeSettingsService());

        var findings = await assistant.ReviewAsync(new Project(), string.Empty);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task ReviewAsync_ParsesReferenceAndFixDroppingInvalidEdits()
    {
        const string json = """{"findings":[{"severity":"Warning","area":"Characters","title":"Age gap","detail":"Aria is 200 but looks 20.","suggestion":"Align the age.","reference":"Aria","fix":{"edits":[{"target":"Character","reference":"Aria","field":"Age","value":"20"},{"target":"Character","reference":"Aria","field":"Bogus","value":"x"}]}}]}""";
        var assistant = new ProjectReviewAssistant(new ScriptedLlmClient(json, []), new FakeSettingsService());

        var findings = await assistant.ReviewAsync(new Project(), string.Empty);

        var finding = Assert.Single(findings);
        Assert.Equal("Aria", finding.Reference);
        Assert.NotNull(finding.Fix);
        var edit = Assert.Single(finding.Fix!.Edits);
        Assert.Equal(GenerationTarget.Character, edit.Target);
        Assert.Equal("Aria", edit.Reference);
        Assert.Equal("Age", edit.Field);
        Assert.Equal("20", edit.Value);
    }

    [Fact]
    public async Task ReviewAsync_MalformedJson_ReturnsEmpty()
    {
        var assistant = new ProjectReviewAssistant(new ScriptedLlmClient("{ not json", []), new FakeSettingsService());

        var findings = await assistant.ReviewAsync(new Project(), string.Empty);

        Assert.Empty(findings);
    }
}
