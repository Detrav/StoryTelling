using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Application.Review;

namespace StoryTelling.ViewModels;

public partial class ReviewEditRowViewModel : ObservableObject
{
    public ReviewEditRowViewModel(string field, string label, string oldValue, string newValue, ReviewEdit? edit)
    {
        Field = field;
        Label = label;
        OldValue = oldValue;
        Edit = edit;
        _newValue = newValue;
    }

    public string Field { get; }

    public string Label { get; }

    public string OldValue { get; }

    public ReviewEdit? Edit { get; }

    [ObservableProperty]
    private string _newValue;

    [ObservableProperty]
    private bool _isSelected = true;

    public ReviewEdit? ToEdit() => Edit is null ? null : Edit with { Value = NewValue };
}
