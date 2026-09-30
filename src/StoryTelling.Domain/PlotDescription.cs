namespace StoryTelling.Domain;

public sealed class PlotDescription
{
    public string Genre { get; set; } = string.Empty;

    public string Tone { get; set; } = string.Empty;

    public string Premise { get; set; } = string.Empty;

    public string Direction { get; set; } = string.Empty;

    public int ChapterCount { get; set; } = 1;
}
