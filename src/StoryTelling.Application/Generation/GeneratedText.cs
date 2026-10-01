namespace StoryTelling.Application.Generation;

public static class GeneratedText
{
    public static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    public static bool IsPlausible(string text) => !text.Any(character => character is '{' or '}' or '[' or ']');

    public static bool LooksTruncated(string text)
    {
        var trimmed = text.TrimEnd();
        if (trimmed.Length == 0)
        {
            return true;
        }

        return trimmed[^1] is not ('.' or '!' or '?' or '…' or '"' or '\'' or '”' or '’' or ')' or ']');
    }

    public static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstBreak = text.IndexOf('\n');
        if (firstBreak < 0)
        {
            return text;
        }

        var body = text[(firstBreak + 1)..];
        var closing = body.LastIndexOf("```", StringComparison.Ordinal);
        return (closing < 0 ? body : body[..closing]).Trim();
    }
}
