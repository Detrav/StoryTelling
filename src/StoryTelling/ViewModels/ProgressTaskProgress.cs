namespace StoryTelling.ViewModels;

public sealed record ProgressTaskProgress(int Completed, int Total, int CurrentIndex, string Stage);
