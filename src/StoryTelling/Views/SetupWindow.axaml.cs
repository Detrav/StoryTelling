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
                (brief, options, cancellationToken) => setup.GenerateAsync(target, brief, options, cancellationToken)),
        };

        var result = await wizard.ShowDialog<IReadOnlyDictionary<string, string>?>(this);
        if (result is { Count: > 0 })
        {
            setup.ApplyGenerated(result);
        }
    }

    private void OnCommit(object? sender, FocusChangedEventArgs e) => (DataContext as SetupViewModel)?.Commit();

    private async void OnAddCharacterClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SetupViewModel setup)
        {
            return;
        }

        var character = new CharacterEditorViewModel();
        character.GenerateOptions = (brief, options, cancellationToken) => setup.GenerateCharacterAsync(character, brief, options, cancellationToken);

        var window = new CharacterWindow { DataContext = character };
        if (await window.ShowDialog<bool>(this))
        {
            setup.AddCharacter(character);
        }
    }

    private async void OnEditCharacterClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SetupViewModel setup || setup.SelectedCharacter is not { } selected)
        {
            return;
        }

        var draft = selected.Clone();
        draft.GenerateOptions = (brief, options, cancellationToken) => setup.GenerateCharacterAsync(draft, brief, options, cancellationToken);

        var window = new CharacterWindow { DataContext = draft };
        if (await window.ShowDialog<bool>(this))
        {
            setup.ApplyCharacterEdit(selected, draft);
        }
    }

    private async void OnDeleteCharacterClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SetupViewModel setup || setup.SelectedCharacter is not { } selected)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(selected.Name) ? "this character" : selected.Name;
        if (await ConfirmDialog.ShowAsync(this, "Delete character", $"Delete \"{name}\"? This cannot be undone."))
        {
            setup.RemoveCharacter(selected);
        }
    }
}
