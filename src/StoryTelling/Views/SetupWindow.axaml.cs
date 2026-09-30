using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
                (brief, options, session, progress, cancellationToken) => setup.GenerateAsync(target, brief, options, session, progress, cancellationToken)),
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
        character.GenerateOptions = (brief, options, session, progress, cancellationToken) => setup.GenerateCharacterAsync(character, brief, options, session, progress, cancellationToken);

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
        draft.GenerateOptions = (brief, options, session, progress, cancellationToken) => setup.GenerateCharacterAsync(draft, brief, options, session, progress, cancellationToken);

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

    private async void OnAddKnowledgeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SetupViewModel setup)
        {
            return;
        }

        var entry = new KnowledgeEntryEditorViewModel();
        var window = new KnowledgeEntryWindow { DataContext = entry };
        if (await window.ShowDialog<bool>(this))
        {
            setup.AddKnowledge(entry);
        }
    }

    private async void OnEditKnowledgeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SetupViewModel setup || setup.SelectedKnowledge is not { } selected)
        {
            return;
        }

        var draft = selected.Clone();
        var window = new KnowledgeEntryWindow { DataContext = draft };
        if (await window.ShowDialog<bool>(this))
        {
            setup.ApplyKnowledgeEdit(selected, draft);
        }
    }

    private async void OnDeleteKnowledgeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SetupViewModel setup || setup.SelectedKnowledge is not { } selected)
        {
            return;
        }

        var title = string.IsNullOrWhiteSpace(selected.Title) ? "this entry" : selected.Title;
        if (await ConfirmDialog.ShowAsync(this, "Delete entry", $"Delete \"{title}\"?"))
        {
            setup.RemoveKnowledge(selected);
        }
    }

    private async void OnImportKnowledgeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SetupViewModel setup)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import markdown",
            AllowMultiple = true,
            FileTypeFilter = [MarkdownFileType],
        });

        if (files.Count == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        foreach (var file in files)
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            builder.AppendLine($"# {file.Name}");
            builder.AppendLine(content);
            builder.AppendLine();
        }

        var source = builder.ToString();
        var viewModel = new KnowledgeImportViewModel((brief, progress, cancellationToken) =>
            setup.ExtractKnowledgeAsync(source, brief, progress, cancellationToken));

        var window = new KnowledgeImportWindow { DataContext = viewModel };
        var result = await window.ShowDialog<IReadOnlyList<KnowledgeEntryEditorViewModel>?>(this);
        if (result is { Count: > 0 })
        {
            setup.AddKnowledgeRange(result);
        }
    }

    private static FilePickerFileType MarkdownFileType => new("Markdown")
    {
        Patterns = ["*.md"],
    };
}
