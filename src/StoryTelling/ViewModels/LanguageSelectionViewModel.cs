using CommunityToolkit.Mvvm.ComponentModel;

namespace StoryTelling.ViewModels;

public partial class LanguageSelectionViewModel : ViewModelBase
{
    public LanguageSelectionViewModel(LanguageOption option, bool isSelected)
    {
        Option = option;
        _isSelected = isSelected;
    }

    public LanguageOption Option { get; }

    public string DisplayName => Option.DisplayName;

    [ObservableProperty]
    private bool _isSelected;
}
