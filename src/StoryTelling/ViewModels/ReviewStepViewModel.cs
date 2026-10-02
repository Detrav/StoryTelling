using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Application.Review;

namespace StoryTelling.ViewModels;

public partial class ReviewStepViewModel : ObservableObject
{
    public ReviewStepViewModel(ReviewCheck check) => Check = check;

    public ReviewCheck Check { get; }

    public string Label => Check.Label;

    public string Description => Check.Intro;

    public ObservableCollection<ReviewFindingViewModel> Findings { get; } = [];

    public string? Signature { get; set; }

    [ObservableProperty]
    private bool _wasRun;

    [ObservableProperty]
    private bool _isStale;

    [ObservableProperty]
    private bool _isSkipped;

    [ObservableProperty]
    private string _status = "Not run yet.";
}
