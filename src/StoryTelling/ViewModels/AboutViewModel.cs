using System.Reflection;

namespace StoryTelling.ViewModels;

public sealed class AboutViewModel : ViewModelBase
{
    private static readonly Assembly _assembly = typeof(AboutViewModel).Assembly;

    public string Version => Display(
        _assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        _assembly.GetName().Version?.ToString());

    public string Copyright =>
        _assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;

    private static string Display(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var plus = candidate.IndexOf('+');
            return plus < 0 ? candidate : candidate[..plus];
        }

        return "1.0.0";
    }
}
