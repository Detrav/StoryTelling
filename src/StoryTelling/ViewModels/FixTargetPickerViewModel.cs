using CommunityToolkit.Mvvm.ComponentModel;

namespace StoryTelling.ViewModels;

public partial class FixTargetPickerViewModel : ObservableObject
{
    public FixTargetPickerViewModel(IReadOnlyList<ReviewFixTarget> targets)
    {
        Targets = targets;
        _selected = targets.FirstOrDefault();
    }

    public IReadOnlyList<ReviewFixTarget> Targets { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ReviewFixTarget? _selected;

    public bool HasSelection => Selected is not null;
}
