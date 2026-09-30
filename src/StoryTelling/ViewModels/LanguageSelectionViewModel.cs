using StoryTelling.Application.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StoryTelling.ViewModels;

public partial class LanguageSelectionViewModel : ViewModelBase
{
    public LanguageSelectionViewModel(LanguageData language, bool isSelected)
    {
        Language = language;
        _isSelected = isSelected;
    }

    public LanguageData Language { get; }

    public string DisplayName => Language.DisplayName;

    [ObservableProperty]
    private bool _isSelected;
}
