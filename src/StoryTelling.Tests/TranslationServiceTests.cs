using StoryTelling.Application.Llm;
using StoryTelling.Application.Translation;

namespace StoryTelling.Tests;

public sealed class TranslationServiceTests
{
    [Fact]
    public async Task TranslateAsync_ReturnsModelText()
    {
        var service = new TranslationService(new FakeLlmClient("Bonjour le monde."), new FakeSettingsService());

        var text = await service.TranslateAsync("Hello world.", "fr");

        Assert.Equal("Bonjour le monde.", text);
    }

    [Fact]
    public async Task TranslateAsync_EmptyInput_ReturnsInputWithoutCallingModel()
    {
        var client = new FakeLlmClient("ignored");
        var service = new TranslationService(client, new FakeSettingsService());

        var text = await service.TranslateAsync("   ", "fr");

        Assert.Equal("   ", text);
        Assert.Null(client.LastRequest);
    }

    [Fact]
    public async Task TranslateAsync_EmptyResponse_Throws()
    {
        var service = new TranslationService(new FakeLlmClient("   "), new FakeSettingsService());

        var exception = await Assert.ThrowsAsync<LlmException>(() => service.TranslateAsync("Hello world.", "fr"));

        Assert.Equal(LlmErrorKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public async Task TranslateAsync_RetriesWhenTruncated()
    {
        var client = new FakeLlmClient("ignored");
        client.CompleteQueue.Enqueue(new LlmCompletion("Bonjour le monde", "length", 1, 1));
        client.CompleteQueue.Enqueue(new LlmCompletion("Bonjour le monde entier.", "stop", 1, 1));
        var service = new TranslationService(client, new FakeSettingsService());

        var text = await service.TranslateAsync("Hello whole world.", "fr");

        Assert.Equal("Bonjour le monde entier.", text);
    }

    [Fact]
    public async Task TranslateAsync_RepairsParagraphsWithWrongScript()
    {
        var client = new FakeLlmClient("ignored");
        client.CompleteQueue.Enqueue(new LlmCompletion(
            "Первый абзац на русском, всё хорошо и понятно.\n\n第二段完全是用中文写的，这不应该出现.",
            "stop", 1, 1));
        client.CompleteQueue.Enqueue(new LlmCompletion("Второй абзац переведён на русский язык.", "stop", 1, 1));
        var service = new TranslationService(client, new FakeSettingsService());

        var text = await service.TranslateAsync("First paragraph.\n\nSecond paragraph.", "ru");

        Assert.DoesNotContain("中", text);
        Assert.Contains("Второй абзац переведён", text);
    }
}
