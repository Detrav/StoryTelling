namespace StoryTelling.ViewModels;

public sealed record BookCompletionProgress(int Completed, int Total, int CurrentIndex, string Stage);