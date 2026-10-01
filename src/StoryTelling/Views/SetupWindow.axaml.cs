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

    private void OnReviewClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SetupViewModel setup)
        {
            return;
        }

        var viewModel = new ProjectReviewViewModel(
            (brief, progress, cancellationToken) => setup.RunReviewAsync(brief, progress, cancellationToken),
            setup);

        var window = new ProjectReviewWindow { DataContext = viewModel };
        window.Show(this);
    }

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

    private async void OnAddKnowledgeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SetupViewModel setup)
        {
            return;
        }

        var entry = new KnowledgeEntryEditorViewModel();
        entry.GenerateOptions = (brief, options, session, progress, cancellationToken) => setup.GenerateKnowledgeAsync(entry, brief, options, session, progress, cancellationToken);
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
        draft.GenerateOptions = (brief, options, session, progress, cancellationToken) => setup.GenerateKnowledgeAsync(draft, brief, options, session, progress, cancellationToken);
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

        var plan = setup.PlanKnowledgeImport(source);
        if (plan.TooLarge)
        {
            ErrorDialog.Show(
                this,
                "File too large",
                $"This file splits into {plan.ChunkCount} chunks, but the import limit is {plan.MaxChunks}. "
                + "Split it into smaller Markdown files and import them one by one.");
            return;
        }

        var viewModel = new KnowledgeImportViewModel(
            (source, brief, progress, cancellationToken) => setup.ExtractKnowledgeAsync(source, brief, progress, cancellationToken),
            initialSource: source,
            existingTitles: setup.KnowledgeTitles);

        var window = new KnowledgeImportWindow { DataContext = viewModel };
        var result = await window.ShowDialog<IReadOnlyList<KnowledgeEntryEditorViewModel>?>(this);
        if (result is { Count: > 0 })
        {
            setup.AddKnowledgeRange(result);
        }
    }

    private async void OnBrainstormKnowledgeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SetupViewModel setup)
        {
            return;
        }

        var viewModel = new KnowledgeImportViewModel(
            (source, brief, progress, cancellationToken) => setup.DesignKnowledgeAsync(source, brief, progress, cancellationToken),
            autoRun: false,
            showSource: true,
            header: "Knowledge from prompt",
            existingTitles: setup.KnowledgeTitles);

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
