using System.Text;
using System.Text.RegularExpressions;
using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public static partial class ChapterTextCleaner
{
    public static string StripLeadingTitle(string text, Chapter chapter)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var index = 0;
        while (index < lines.Length && string.IsNullOrWhiteSpace(lines[index]))
        {
            index++;
        }

        if (index >= lines.Length || !IsTitleLine(lines[index], chapter))
        {
            return text;
        }

        return string.Join('\n', lines.Skip(index + 1)).TrimStart('\n');
    }

    private static bool IsTitleLine(string line, Chapter chapter)
    {
        if (HeadingOnly().IsMatch(line))
        {
            return true;
        }

        var normalized = Normalize(line);
        if (normalized.Length == 0)
        {
            return false;
        }

        var title = Normalize(chapter.Title);
        if (title.Length > 0 && normalized == title)
        {
            return true;
        }

        var withoutHeading = Normalize(HeadingPrefix().Replace(line, string.Empty));
        return title.Length > 0 && withoutHeading == title;
    }

    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"^\s*#*\s*chapter\s+\d+\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingOnly();

    [GeneratedRegex(@"^\s*#*\s*chapter\s+\d+\s*[:.\-–—]?\s*", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingPrefix();
}
