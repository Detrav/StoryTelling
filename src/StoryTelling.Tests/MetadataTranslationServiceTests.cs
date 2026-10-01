using StoryTelling.Application.Llm;
using StoryTelling.Application.Translation;

namespace StoryTelling.Tests;

public sealed class MetadataTranslationServiceTests
{
    [Fact]
    public async Task TranslateAsync_ParsesStructuredJson()
    {
        const string json = """
            {"name":"Корона","annotation":"Аннотация.","chapterTitles":[{"number":1,"title":"Угли"},{"number":2,"title":"Пепел"}]}
            """;
        var service = new MetadataTranslationService(new FakeLlmClient(json), new FakeSettingsService());

        var result = await service.TranslateAsync(Request());

        Assert.Equal("Корона", result.BookName);
        Assert.Equal("Аннотация.", result.Annotation);
        Assert.Equal("Угли", result.ChapterTitles[1]);
        Assert.Equal("Пепел", result.ChapterTitles[2]);
    }

    [Fact]
    public async Task TranslateAsync_MissingFields_FallBackToSource()
    {
        const string json = """{"name":"Корона","annotation":"","chapterTitles":[]}""";
        var service = new MetadataTranslationService(new FakeLlmClient(json), new FakeSettingsService());

        var result = await service.TranslateAsync(Request());

        Assert.Equal("Корона", result.BookName);
        Assert.Equal("A dying empire.", result.Annotation);
        Assert.Empty(result.ChapterTitles);
    }

    [Fact]
    public async Task TranslateAsync_EmptyResponse_Throws()
    {
        const string json = """{"name":"","annotation":"","chapterTitles":[]}""";
        var service = new MetadataTranslationService(new FakeLlmClient(json), new FakeSettingsService());

        var exception = await Assert.ThrowsAsync<LlmException>(() => service.TranslateAsync(Request()));

        Assert.Equal(LlmErrorKind.InvalidResponse, exception.Kind);
    }

    private static MetadataTranslationRequest Request() => new(
        "ru",
        "The Ember Crown",
        "A dying empire.",
        [new MetadataChapterTitle(1, "Embers"), new MetadataChapterTitle(2, "Ash")]);
}
