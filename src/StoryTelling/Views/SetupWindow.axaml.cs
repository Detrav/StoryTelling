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

        var target = SetupViewModel.MapTarget(field);
        var wizard = new AiWizardWindow
        {
            DataContext = new AiWizardViewModel(
                setup.LabelFor(field),
                target,
                (brief, cancellationToken) => setup.GenerateAsync(target, brief, cancellationToken)),
        };

        var result = await wizard.ShowDialog<IReadOnlyDictionary<string, string>?>(this);
        if (result is { Count: > 0 })
        {
            setup.ApplyGenerated(result);
        }
    }

    private void OnCommit(object? sender, FocusChangedEventArgs e) => (DataContext as SetupViewModel)?.Commit();
}
