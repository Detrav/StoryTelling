namespace StoryTelling.Cli;

internal static class ArgReader
{
    public static string? Value(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    public static string String(IReadOnlyList<string> args, string name, string fallback) => Value(args, name) ?? fallback;

    public static int Int(IReadOnlyList<string> args, string name, int fallback) =>
        int.TryParse(Value(args, name), out var value) ? value : fallback;

    public static double Double(IReadOnlyList<string> args, string name, double fallback) =>
        double.TryParse(Value(args, name), System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
