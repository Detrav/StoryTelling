using StoryTelling.Application.Generation;
using StoryTelling.Application.Settings;

namespace StoryTelling.Tests;

public sealed class GenerationAssistantTests
{
    [Fact]
    public async Task GenerateAsync_ParsesObjectArrayAndUsesSettings()
    {
        var llm = new FakeLlmClient("""[{"title":"T1","body":"B1"},{"title":"T2","body":"B2"}]""");
        var settings = new FakeSettingsService
        {
            Settings = new AppSettings { Model = "m", BaseUrl = "http://127.0.0.1:1234", ApiKey = "k" },
        };
        var assistant = new GenerationAssistant(llm, settings);

        var options = await assistant.GenerateAsync(Request());

        Assert.Equal(2, options.Count);
        Assert.Equal("T1", options[0].Fields["WorldTitle"]);
        Assert.Equal("B1", options[0].Fields["WorldBody"]);
        Assert.Equal("T2", options[1].Fields["WorldTitle"]);
        Assert.Equal("m", llm.LastRequest!.Model);
        Assert.Equal("http://127.0.0.1:1234", llm.LastConnection!.BaseUrl);
        Assert.Equal("WorldOptions", llm.LastSchemaName);
        Assert.Equal(2, llm.LastSchema!["minItems"]!.GetValue<int>());
    }

    [Fact]
    public async Task GenerateAsync_StripsCodeFences()
    {
        var llm = new FakeLlmClient("```json\n[{\"title\":\"T\",\"body\":\"B\"}]\n```");
        var assistant = new GenerationAssistant(llm, new FakeSettingsService());

        var options = await assistant.GenerateAsync(Request());

        Assert.Single(options);
        Assert.Equal("T", options[0].Fields["WorldTitle"]);
    }

    [Fact]
    public async Task GenerateAsync_NonJson_SingleFieldFallsBackToText()
    {
        var llm = new FakeLlmClient("Just prose without JSON.");
        var assistant = new GenerationAssistant(llm, new FakeSettingsService());

        var options = await assistant.GenerateAsync(Request(GenerationTarget.ProjectName));

        Assert.Single(options);
        Assert.Equal("Just prose without JSON.", options[0].Fields["ProjectName"]);
    }

    [Fact]
    public async Task GenerateAsync_NonJson_GroupTargetReturnsNoOptions()
    {
        var llm = new FakeLlmClient("Just prose without JSON.");
        var assistant = new GenerationAssistant(llm, new FakeSettingsService());

        var options = await assistant.GenerateAsync(Request());

        Assert.Empty(options);
    }

    [Fact]
    public async Task GenerateAsync_PassesBriefAndCurrentDraft()
    {
        var llm = new FakeLlmClient("[]");
        var assistant = new GenerationAssistant(llm, new FakeSettingsService());
        var request = Request() with
        {
            Brief = "grim and cold",
            Context = new GenerationContext
            {
                Fields = new Dictionary<string, string> { ["ProjectName"] = "Book", ["WorldBody"] = "old body" },
            },
        };

        await assistant.GenerateAsync(request);

        var userMessage = llm.LastRequest!.Messages[1].Content;
        Assert.Contains("grim and cold", userMessage);
        Assert.Contains("Current draft", userMessage);
        Assert.Contains("old body", userMessage);
    }

    private static GenerationRequest Request(GenerationTarget target = GenerationTarget.World) => new()
    {
        Target = target,
        Variants = 2,
        Context = new GenerationContext
        {
            Fields = new Dictionary<string, string> { ["ProjectName"] = "Book", ["WorldTitle"] = "Ashen Reach" },
        },
    };
}
