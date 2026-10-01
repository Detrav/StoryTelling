namespace StoryTelling.ViewModels;

public sealed record ExportLanguage(string Code, string DisplayName, int Translated, int Total)
{
    public bool IsOriginal => string.Equals(Code, "en", StringComparison.OrdinalIgnoreCase);

    public string Coverage => IsOriginal
        ? "original"
        : Total == 0 ? "no chapters" : $"{Translated}/{Total} translated";
}
