using System.Text.RegularExpressions;

namespace StoryTelling.Application.Chapters;

public static class StyleGuard
{
    private static readonly Regex _chapterReference = new(
        @"\bchapter\s+(\d+|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] _phrases =
    [
        "this chapter",
        "that chapter",
        "the previous chapter",
        "the next chapter",
        "in the story so far",
        "the story so far",
        "as we have seen",
        "as previously mentioned",
    ];

    private static readonly string[] _presentMarkers = [" is ", " are ", " am ", " has ", " have ", " does ", " do "];

    private static readonly string[] _pastMarkers = [" was ", " were ", " had ", " did "];

    public static IReadOnlyList<string> FindViolations(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var violations = new List<string>();
        foreach (Match match in _chapterReference.Matches(text))
        {
            violations.Add(match.Value.Trim());
        }

        var lower = text.ToLowerInvariant();
        foreach (var phrase in _phrases)
        {
            if (lower.Contains(phrase, StringComparison.Ordinal))
            {
                violations.Add(phrase);
            }
        }

        return violations.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool HasMeta(string? text) => FindViolations(text ?? string.Empty).Count > 0;

    public static string RemoveMeta(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var result = _chapterReference.Replace(text, string.Empty);
        foreach (var phrase in _phrases)
        {
            result = Regex.Replace(result, Regex.Escape(phrase), string.Empty, RegexOptions.IgnoreCase);
        }

        return Regex.Replace(result, @"\s{2,}", " ").Trim();
    }

    public static bool DriftsFromTense(string text, string declaredTense)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(declaredTense))
        {
            return false;
        }

        var declared = declaredTense.ToLowerInvariant();
        var expectsPresent = declared.Contains("present", StringComparison.Ordinal);
        var expectsPast = declared.Contains("past", StringComparison.Ordinal);
        if (expectsPresent == expectsPast)
        {
            return false;
        }

        var lower = text.ToLowerInvariant();
        var present = CountMarkers(lower, _presentMarkers);
        var past = CountMarkers(lower, _pastMarkers);

        return expectsPresent
            ? past >= 5 && past > present * 2
            : present >= 5 && present > past * 2;
    }

    private static int CountMarkers(string text, string[] markers)
    {
        var total = 0;
        foreach (var marker in markers)
        {
            total += Regex.Matches(text, Regex.Escape(marker), RegexOptions.IgnoreCase).Count;
        }

        return total;
    }
}
