namespace StoryTelling.Application.Story;

public sealed record StoryOverview(
    string Name,
    string Genre,
    string Tone,
    string Style,
    string PointOfView,
    string Tense,
    string Rating,
    string WorldTitle,
    string WorldBody);
