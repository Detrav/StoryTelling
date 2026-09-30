using System.Text;
using System.Text.RegularExpressions;

namespace StoryTelling.Application.Retrieval;

public static class KnowledgeChunker
{
    public const int DefaultMaxChars = 800;

    private static readonly Regex _heading = new("^#{1,6}\\s", RegexOptions.Compiled);

    public static IReadOnlyList<string> Split(string content, int maxChars = DefaultMaxChars)
    {
        if (string.IsNullOrWhiteSpace(content) || maxChars <= 0)
        {
            return [];
        }

        var normalized = content.Replace("\r\n", "\n");
        var fragments = new List<string>();
        foreach (var section in SplitSections(normalized))
        {
            fragments.AddRange(ChunkParagraphs(section, maxChars));
        }

        return fragments;
    }

    private static List<string> SplitSections(string text)
    {
        var sections = new List<string>();
        var builder = new StringBuilder();

        foreach (var line in text.Split('\n'))
        {
            if (builder.Length > 0 && _heading.IsMatch(line))
            {
                sections.Add(builder.ToString().Trim());
                builder.Clear();
            }

            builder.AppendLine(line);
        }

        if (!string.IsNullOrWhiteSpace(builder.ToString()))
        {
            sections.Add(builder.ToString().Trim());
        }

        return sections;
    }

    private static IEnumerable<string> ChunkParagraphs(string text, int maxChars)
    {
        var paragraphs = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var fragments = new List<string>();
        var buffer = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            foreach (var piece in SplitLong(paragraph, maxChars))
            {
                if (buffer.Length > 0 && buffer.Length + piece.Length + 2 > maxChars)
                {
                    fragments.Add(buffer.ToString().Trim());
                    buffer.Clear();
                }

                if (buffer.Length > 0)
                {
                    buffer.Append("\n\n");
                }

                buffer.Append(piece);
            }
        }

        if (buffer.Length > 0)
        {
            fragments.Add(buffer.ToString().Trim());
        }

        return fragments;
    }

    private static IEnumerable<string> SplitLong(string paragraph, int maxChars)
    {
        if (paragraph.Length <= maxChars)
        {
            yield return paragraph;
            yield break;
        }

        for (var index = 0; index < paragraph.Length; index += maxChars)
        {
            yield return paragraph.Substring(index, Math.Min(maxChars, paragraph.Length - index));
        }
    }
}
