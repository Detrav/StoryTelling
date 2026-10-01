using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

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

    [Fact]
    public async Task GenerateAsync_WithSnapshot_GathersViaToolsThenReturnsOptions()
    {
        var client = new ScriptedLlmClient(
            jsonResponse: """[{"title":"T","body":"B"}]""",
            toolResponses:
            [
                new LlmToolResponse(string.Empty, "tool_calls", [new LlmToolCall("c1", "characters", "{}")]),
                new LlmToolResponse(string.Empty, "stop", []),
            ]);
        var assistant = new GenerationAssistant(client, new FakeSettingsService
        {
            Settings = new AppSettings { Model = "m", MaxToolCalls = 5 },
        });
        var request = Request(GenerationTarget.World) with
        {
            Variants = 1,
            Snapshot = new Project { Knowledge = [new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Aria", Tags = ["protagonist"] }] },
        };

        var options = await assistant.GenerateAsync(request);

        Assert.Single(options);
        Assert.NotNull(client.LastJsonRequest);
        Assert.Contains(client.LastJsonRequest!.Messages, message => message.Role == LlmRole.Tool);
        Assert.Contains(client.ToolRequests[0].Messages, message => message.Content.Contains("Consult the project with the tools"));
    }

    [Fact]
    public async Task GenerateAsync_Knowledge_JoinsArrayTags()
    {
        const string json = """[{"kind":"Place","title":"Ashen Reach","tags":["region","cold"],"content":"A frozen frontier."}]""";
        var assistant = new GenerationAssistant(new FakeLlmClient(json), new FakeSettingsService());

        var options = await assistant.GenerateAsync(Request(GenerationTarget.Knowledge));

        Assert.Equal("region, cold", Assert.Single(options).Fields["Tags"]);
    }

    [Fact]
    public async Task GenerateAsync_RejectsStructuralJunkAndRetries()
    {
        const string junk = """[{"kind":"Place","title":"Ashen","tags":["},{"}]""";
        const string good = """[{"kind":"Place","title":"Ashen Reach","tags":["region"],"content":"A frozen frontier."}]""";
        var llm = new FakeLlmClient(good);
        llm.JsonQueue.Enqueue(junk);
        var assistant = new GenerationAssistant(llm, new FakeSettingsService());

        var options = await assistant.GenerateAsync(Request(GenerationTarget.Knowledge));

        Assert.Equal("Ashen Reach", Assert.Single(options).Fields["Title"]);
        Assert.Equal(2, llm.JsonCallCount);
    }

    [Fact]
    public async Task GenerateAsync_AllAttemptsInvalid_ReturnsEmpty()
    {
        var llm = new FakeLlmClient("""[{"kind":"Place","title":"Ashen","tags":["},{"}]""");
        var assistant = new GenerationAssistant(llm, new FakeSettingsService());

        var options = await assistant.GenerateAsync(Request(GenerationTarget.Knowledge));

        Assert.Empty(options);
        Assert.Equal(GenerationAssistant.MaxStructuredAttempts, llm.JsonCallCount);
    }

    [Fact]
    public async Task GenerateAsync_Knowledge_ParsesKindAndFields()
    {
        const string json = """[{"kind":"Place","title":"Ashen Reach","tags":"region, cold","content":"A frozen frontier."}]""";
        var client = new ScriptedLlmClient(json,
        [
            new LlmToolResponse(string.Empty, "tool_calls", [new LlmToolCall("c1", "list_entries", "{}")]),
            new LlmToolResponse(string.Empty, "stop", []),
        ]);
        var assistant = new GenerationAssistant(client, new FakeSettingsService
        {
            Settings = new AppSettings { Model = "m", MaxToolCalls = 5 },
        });
        var request = new GenerationRequest
        {
            Target = GenerationTarget.Knowledge,
            Variants = 1,
            Context = new GenerationContext { Fields = new Dictionary<string, string> { ["ProjectName"] = "Book" } },
            Snapshot = new Project(),
        };

        var options = await assistant.GenerateAsync(request);

        var fields = Assert.Single(options).Fields;
        Assert.Equal("Place", fields["Kind"]);
        Assert.Equal("Ashen Reach", fields["Title"]);
        Assert.Equal("region, cold", fields["Tags"]);
        Assert.Equal("A frozen frontier.", fields["Content"]);
    }

    [Fact]
    public async Task GenerateAsync_FiltersTitlesFromAvoidList()
    {
        const string json = """[{"kind":"Character","title":"Aria","tags":[],"content":"a"},{"kind":"Character","title":"Bran","tags":[],"content":"b"}]""";
        var assistant = new GenerationAssistant(new FakeLlmClient(json), new FakeSettingsService());

        var options = await assistant.GenerateAsync(Request(GenerationTarget.Knowledge) with { Variants = 2, Avoid = ["Aria"] });

        Assert.Equal("Bran", Assert.Single(options).Fields["Title"]);
    }

    [Fact]
    public async Task GenerateAsync_DeduplicatesOptions()
    {
        const string json = """[{"kind":"Character","title":"Bran","tags":[],"content":"b"},{"kind":"Character","title":"bran","tags":[],"content":"b2"}]""";
        var assistant = new GenerationAssistant(new FakeLlmClient(json), new FakeSettingsService());

        var options = await assistant.GenerateAsync(Request(GenerationTarget.Knowledge) with { Variants = 2 });

        Assert.Single(options);
    }

    [Fact]
    public async Task GenerateAsync_AllOptionsAvoided_RetriesThenReturnsEmpty()
    {
        var llm = new FakeLlmClient("""[{"kind":"Character","title":"Aria","tags":[],"content":"a"}]""");
        var assistant = new GenerationAssistant(llm, new FakeSettingsService());

        var options = await assistant.GenerateAsync(Request(GenerationTarget.Knowledge) with { Variants = 1, Avoid = ["Aria"] });

        Assert.Empty(options);
        Assert.Equal(GenerationAssistant.MaxStructuredAttempts, llm.JsonCallCount);
    }

    [Fact]
    public async Task GenerateAsync_ChapterSettings_IncludesPreviousChapters()
    {
        var llm = new FakeLlmClient("[]");
        var assistant = new GenerationAssistant(llm, new FakeSettingsService());
        var project = new Project
        {
            Chapters =
            [
                new Chapter { Number = 1, Title = "Embers", Logline = "A scout flees the keep.", ContentOriginal = "text", WorldState = new WorldState { TimeAndPlace = "Dusk above the keep" } },
            ],
        };
        var request = new GenerationRequest
        {
            Target = GenerationTarget.ChapterSettings,
            Variants = 1,
            Context = new GenerationContext(),
            Snapshot = project,
        };

        await assistant.GenerateAsync(request);

        var user = llm.LastRequest!.Messages[1].Content;
        Assert.Contains("Previous chapters", user);
        Assert.Contains("A scout flees the keep.", user);
        Assert.Contains("Dusk above the keep", user);
    }

    [Fact]
    public async Task GenerateAsync_ChapterPlan_ForcesResolutionInLastChapter()
    {
        var llm = new FakeLlmClient("[]");
        var assistant = new GenerationAssistant(llm, new FakeSettingsService());

        await assistant.GenerateAsync(new GenerationRequest
        {
            Target = GenerationTarget.ChapterPlan,
            Variants = 5,
            Context = new GenerationContext(),
        });

        var user = llm.LastRequest!.Messages[1].Content;
        Assert.Contains("exactly 5 chapters", user);
        Assert.Contains("FULL resolution in chapter 5", user);
    }

    [Fact]
    public async Task GenerateAsync_WithoutSnapshot_DoesNotUseTools()
    {
        var client = new ScriptedLlmClient("""[{"title":"T","body":"B"}]""", []);
        var assistant = new GenerationAssistant(client, new FakeSettingsService());

        await assistant.GenerateAsync(Request(GenerationTarget.World) with { Variants = 1 });

        Assert.Empty(client.ToolRequests);
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
