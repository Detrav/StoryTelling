using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Settings;

namespace StoryTelling.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void TemperatureFor_CreativeTask_FollowsGlobalTemperature()
    {
        var settings = new AppSettings { Temperature = 0.9 };

        Assert.Equal(0.9, settings.TemperatureFor(LlmTask.Writer));
        Assert.Equal(0.9, settings.TemperatureFor(LlmTask.Planner));
    }

    [Fact]
    public void TemperatureFor_DeterministicTask_UsesItsDefault()
    {
        var settings = new AppSettings { Temperature = 0.9 };

        Assert.Equal(0.2, settings.TemperatureFor(LlmTask.Translation));
        Assert.Equal(0.3, settings.TemperatureFor(LlmTask.Summarizer));
    }

    [Fact]
    public void TemperatureFor_Override_WinsOverDefault()
    {
        var settings = new AppSettings { Temperature = 0.9 };
        settings.RoleTemperatures[LlmTask.Writer.Id()] = 0.4;
        settings.RoleTemperatures[LlmTask.Translation.Id()] = 0.05;

        Assert.Equal(0.4, settings.TemperatureFor(LlmTask.Writer));
        Assert.Equal(0.05, settings.TemperatureFor(LlmTask.Translation));
    }

    [Fact]
    public void ReasoningEffortFor_ReviewDefaultsToNone_AndOthersToProviderDefault()
    {
        var settings = new AppSettings();

        Assert.Equal("none", settings.ReasoningEffortFor(LlmTask.Review));
        Assert.Equal("none", settings.ReasoningEffortFor(LlmTask.Continuity));
        Assert.Equal(string.Empty, settings.ReasoningEffortFor(LlmTask.Writer));
    }

    [Fact]
    public void ReasoningEffortFor_OverrideWinsEvenWhenEmpty()
    {
        var settings = new AppSettings();
        settings.RoleReasoningEfforts[LlmTask.Review.Id()] = "low";
        settings.RoleReasoningEfforts[LlmTask.Continuity.Id()] = string.Empty;

        Assert.Equal("low", settings.ReasoningEffortFor(LlmTask.Review));
        Assert.Equal(string.Empty, settings.ReasoningEffortFor(LlmTask.Continuity));
    }

    [Fact]
    public void ForGenerationTarget_MapsPlannerAndSetupTargets()
    {
        Assert.Equal(LlmTask.Planner, LlmTasks.ForGenerationTarget(GenerationTarget.ChapterPlan));
        Assert.Equal(LlmTask.Planner, LlmTasks.ForGenerationTarget(GenerationTarget.Finale));
        Assert.Equal(LlmTask.Setup, LlmTasks.ForGenerationTarget(GenerationTarget.World));
        Assert.Equal(LlmTask.Setup, LlmTasks.ForGenerationTarget(GenerationTarget.Knowledge));
    }
}
