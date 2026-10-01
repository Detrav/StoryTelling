using System.Linq;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Settings;
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
    public async Task TranslateAsync_TrimsTranslatedValues()
    {
        const string json = """
            {"name":"  Корона  ","annotation":"\nАннотация.\n","chapterTitles":[{"number":1,"title":"  Угли  "}]}
            """;
        var service = new MetadataTranslationService(new FakeLlmClient(json), new FakeSettingsService());

        var result = await service.TranslateAsync(Request());

        Assert.Equal("Корона", result.BookName);
        Assert.Equal("Аннотация.", result.Annotation);
        Assert.Equal("Угли", result.ChapterTitles[1]);
    }

    [Fact]
    public async Task TranslateAsync_OmittedField_StaysBlankSoCoverageSeesTheGap()
    {
        const string json = """{"name":"Корона","annotation":"","chapterTitles":[]}""";
        var service = new MetadataTranslationService(new FakeLlmClient(json), new FakeSettingsService());

        var result = await service.TranslateAsync(Request());

        Assert.Equal("Корона", result.BookName);
        Assert.Equal(string.Empty, result.Annotation);
        Assert.Empty(result.ChapterTitles);
    }

    [Fact]
    public async Task TranslateAsync_OmittedName_StaysBlank()
    {
        const string json = """{"name":"","annotation":"Аннотация.","chapterTitles":[]}""";
        var service = new MetadataTranslationService(new FakeLlmClient(json), new FakeSettingsService());

        var result = await service.TranslateAsync(Request());

        Assert.Equal(string.Empty, result.BookName);
        Assert.Equal("Аннотация.", result.Annotation);
    }

    [Fact]
    public async Task TranslateAsync_InvalidChapterEntries_AreSkipped()
    {
        const string json = """
            {"name":"Корона","annotation":"Аннотация.","chapterTitles":[
              "nonsense",
              {"title":"Без номера"},
              {"number":0,"title":"Ноль"},
              {"number":-3,"title":"Минус"},
              {"number":"1","title":"Строка вместо числа"},
              {"number":4,"title":"   "},
              {"number":5,"title":"Пять"}
            ]}
            """;
        var service = new MetadataTranslationService(new FakeLlmClient(json), new FakeSettingsService());

        var result = await service.TranslateAsync(Request());

        Assert.Equal("Пять", Assert.Single(result.ChapterTitles).Value);
        Assert.Equal(5, result.ChapterTitles.Keys.Single());
    }

    [Fact]
    public async Task TranslateAsync_DuplicateNumber_KeepsLastEntry()
    {
        const string json = """
            {"name":"Корона","annotation":"Аннотация.","chapterTitles":[
              {"number":1,"title":"Первый"},
              {"number":1,"title":"Второй"}
            ]}
            """;
        var service = new MetadataTranslationService(new FakeLlmClient(json), new FakeSettingsService());

        var result = await service.TranslateAsync(Request());

        Assert.Equal("Второй", result.ChapterTitles[1]);
    }

    [Fact]
    public async Task TranslateAsync_EmptyResponse_Throws()
    {
        const string json = """{"name":"","annotation":"","chapterTitles":[]}""";
        var service = new MetadataTranslationService(new FakeLlmClient(json), new FakeSettingsService());

        var exception = await Assert.ThrowsAsync<LlmException>(() => service.TranslateAsync(Request()));

        Assert.Equal(LlmErrorKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public async Task TranslateAsync_MalformedJson_Throws()
    {
        var service = new MetadataTranslationService(new FakeLlmClient("{ not json"), new FakeSettingsService());

        var exception = await Assert.ThrowsAsync<LlmException>(() => service.TranslateAsync(Request()));

        Assert.Equal(LlmErrorKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public async Task TranslateAsync_NonObjectRoot_Throws()
    {
        var service = new MetadataTranslationService(new FakeLlmClient("[]"), new FakeSettingsService());

        var exception = await Assert.ThrowsAsync<LlmException>(() => service.TranslateAsync(Request()));

        Assert.Equal(LlmErrorKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public async Task TranslateAsync_UsesStructuredSchemaAndLowTemperature()
    {
        var client = new FakeLlmClient("""{"name":"Корона","annotation":"Аннотация.","chapterTitles":[]}""");
        var settings = new FakeSettingsService { Settings = AppSettings.CreateDefault() };
        settings.Settings.Temperature = 0.9;
        var service = new MetadataTranslationService(client, settings);

        await service.TranslateAsync(Request());

        Assert.Equal("MetadataTranslation", client.LastSchemaName);
        Assert.NotNull(client.LastSchema);
        Assert.Equal(0.2, client.LastRequest!.Temperature);
        Assert.Contains("Embers", string.Join(' ', client.LastRequest.Messages.Select(message => message.Content)));
    }

    private static MetadataTranslationRequest Request() => new(
        "ru",
        "The Ember Crown",
        "A dying empire.",
        [new MetadataChapterTitle(1, "Embers"), new MetadataChapterTitle(2, "Ash")]);
}
