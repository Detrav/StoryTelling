using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StoryTelling.ViewModels;

public partial class AiWizardViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _brief = string.Empty;

    [ObservableProperty]
    private string _edits = string.Empty;

    [ObservableProperty]
    private string? _selectedOption;

    public string Header { get; }

    public ObservableCollection<string> Options { get; } = [];

    public string? Result => string.IsNullOrWhiteSpace(Edits) ? SelectedOption : Edits;

    public AiWizardViewModel(string label)
    {
        Header = $"Generate with AI — {label}";
        MoreOptions();
    }

    [RelayCommand]
    private void MoreOptions()
    {
        var seed = string.IsNullOrWhiteSpace(Brief) ? "the story" : Brief.Trim();

        Options.Clear();
        Options.Add($"(A) {seed}: grounded and direct.");
        Options.Add($"(B) {seed}: darker, higher stakes.");
        Options.Add($"(C) {seed}: lighter, character-focused.");
        SelectedOption = Options[0];
    }
}
