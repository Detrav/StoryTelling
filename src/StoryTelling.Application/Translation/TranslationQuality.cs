using System.Text.RegularExpressions;

namespace StoryTelling.Application.Translation;

public static partial class TranslationQuality
{
    private const int MinimumLettersToJudge = 12;

    private const int MinimumForeignLetters = 3;

    public static List<string> SplitParagraphs(string text) =>
        [.. ParagraphSeparator().Split(text).Select(paragraph => paragraph.Trim()).Where(paragraph => paragraph.Length > 0)];

    public static IReadOnlyList<int> SuspectParagraphs(IReadOnlyList<string> paragraphs, CharScript expected)
    {
        if (expected == CharScript.None)
        {
            return [];
        }

        var suspects = new List<int>();
        for (var index = 0; index < paragraphs.Count; index++)
        {
            if (IsSuspect(paragraphs[index], expected))
            {
                suspects.Add(index);
            }
        }

        return suspects;
    }

    public static bool IsSuspect(string paragraph, CharScript expected)
    {
        if (expected == CharScript.None)
        {
            return false;
        }

        int expectedCount = 0, foreign = 0, total = 0;
        foreach (var character in paragraph)
        {
            var script = Classify(character);
            if (script == CharScript.None)
            {
                continue;
            }

            total++;
            if (script == expected)
            {
                expectedCount++;
            }
            else if (script != CharScript.Latin)
            {
                foreign++;
            }
        }

        if (total < MinimumLettersToJudge)
        {
            return false;
        }

        if (foreign >= MinimumForeignLetters && foreign * 10 > total)
        {
            return true;
        }

        return expected != CharScript.Latin && expectedCount == 0;
    }

    public static CharScript Classify(char character)
    {
        if (character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '\u00C0' and <= '\u024F')
        {
            return CharScript.Latin;
        }

        if (character is >= '\u0400' and <= '\u04FF' or >= '\u0500' and <= '\u052F')
        {
            return CharScript.Cyrillic;
        }

        if (character is >= '\u4E00' and <= '\u9FFF' or >= '\u3040' and <= '\u30FF' or >= '\uAC00' and <= '\uD7AF' or >= '\uF900' and <= '\uFAFF')
        {
            return CharScript.Cjk;
        }

        if (character is >= '\u0600' and <= '\u06FF' or >= '\u0750' and <= '\u077F')
        {
            return CharScript.Arabic;
        }

        if (character is >= '\u0900' and <= '\u097F')
        {
            return CharScript.Devanagari;
        }

        return char.IsLetter(character) ? CharScript.Other : CharScript.None;
    }

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex ParagraphSeparator();
}
