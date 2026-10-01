using StoryTelling.Application.Translation;

namespace StoryTelling.Tests;

public sealed class TranslationQualityTests
{
    [Fact]
    public void SplitParagraphs_SplitsOnBlankLines()
    {
        var paragraphs = TranslationQuality.SplitParagraphs("one\n\ntwo\n\n\nthree");

        Assert.Equal(["one", "two", "three"], paragraphs);
    }

    [Fact]
    public void Suspect_DetectsForeignScriptInRussian()
    {
        Assert.True(TranslationQuality.IsSuspect("第二段完全是用中文写的，这不应该出现。", CharScript.Cyrillic));
    }

    [Fact]
    public void Suspect_IgnoresLatinNamesInRussian()
    {
        const string paragraph = "Мира Вейл спустилась к руинам старого города и нашла Relic у стены.";

        Assert.False(TranslationQuality.IsSuspect(paragraph, CharScript.Cyrillic));
    }

    [Fact]
    public void Suspect_FullyUntranslatedParagraph()
    {
        const string paragraph = "This whole paragraph is still in English and was not translated at all.";

        Assert.True(TranslationQuality.IsSuspect(paragraph, CharScript.Cyrillic));
    }

    [Fact]
    public void Suspect_LatinTarget_FlagsCyrillic()
    {
        const string paragraph = "Этот абзац остался на русском языке и не был переведён совсем.";

        Assert.True(TranslationQuality.IsSuspect(paragraph, CharScript.Latin));
    }

    [Fact]
    public void Suspect_UnknownScript_IsSkipped()
    {
        Assert.False(TranslationQuality.IsSuspect("第二段完全是用中文写的，这不应该出现。", CharScript.None));
    }
}
