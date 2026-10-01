using StoryTelling.Application.Translation;

namespace StoryTelling.ViewModels;

public sealed record ExportLanguage(
    string Code,
    string DisplayName,
    int Translated,
    int Total,
    MetadataCoverage Metadata)
{
    public bool IsOriginal => string.Equals(Code, "en", StringComparison.OrdinalIgnoreCase);

    public string Coverage => IsOriginal
        ? "original"
        : Total == 0 ? "no chapters" : $"{Translated}/{Total} translated";

    public bool MetadataComplete => IsOriginal || Metadata.IsComplete;

    public bool MetadataOnlyStale => !IsOriginal
        && Metadata.IsStale
        && Metadata.HasBookTitle
        && Metadata.HasAnnotation
        && Metadata.TranslatedTitles >= Metadata.TotalTitles;

    public string MetadataCoverage
    {
        get
        {
            if (IsOriginal)
            {
                return "original";
            }

            if (Metadata.IsComplete)
            {
                return "metadata complete";
            }

            var parts = new List<string>();
            if (!Metadata.HasBookTitle)
            {
                parts.Add("book title");
            }

            if (!Metadata.HasAnnotation)
            {
                parts.Add("annotation");
            }

            if (Metadata.TranslatedTitles < Metadata.TotalTitles)
            {
                parts.Add($"chapter titles {Metadata.TranslatedTitles}/{Metadata.TotalTitles}");
            }

            if (Metadata.IsStale)
            {
                parts.Add("out of date");
            }

            return "needs: " + string.Join(", ", parts);
        }
    }
}
