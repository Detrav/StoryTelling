using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class SetupWindow : Window
{
    public SetupWindow()
    {
        InitializeComponent();
        UndoRedoKeyboard.Attach(this);
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private async void OnGenerateClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string field } || DataContext is not SetupViewModel setup)
        {
            return;
        }

        var wizard = new AiWizardWindow { DataContext = new AiWizardViewModel(setup.LabelFor(field)) };
        var result = await wizard.ShowDialog<string?>(this);
        if (!string.IsNullOrWhiteSpace(result))
        {
            setup.ApplyGenerated(field, result);
        }
    }

    private void OnCommit(object? sender, FocusChangedEventArgs e) => (DataContext as SetupViewModel)?.Commit();
}
