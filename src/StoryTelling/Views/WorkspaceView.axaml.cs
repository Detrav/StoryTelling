using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.Domain;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class WorkspaceView : UserControl
{
    public WorkspaceView() => InitializeComponent();

    private void OnDeleteChapterClick(object? sender, RoutedEventArgs e) => Execute(vm => vm.DeleteChapterCommand.Execute(Chapter(sender)));

    private void OnMoveChapterUpClick(object? sender, RoutedEventArgs e) => Execute(vm => vm.MoveChapterUpCommand.Execute(Chapter(sender)));

    private void OnMoveChapterDownClick(object? sender, RoutedEventArgs e) => Execute(vm => vm.MoveChapterDownCommand.Execute(Chapter(sender)));

    private void OnSetStatusDraftClick(object? sender, RoutedEventArgs e) => SetStatus(sender, ChapterStatus.Draft);

    private void OnSetStatusGeneratedClick(object? sender, RoutedEventArgs e) => SetStatus(sender, ChapterStatus.Generated);

    private void OnSetStatusStaleClick(object? sender, RoutedEventArgs e) => SetStatus(sender, ChapterStatus.Stale);

    private void SetStatus(object? sender, ChapterStatus status) =>
        Execute(vm => vm.SetChapterStatus(Chapter(sender), status));

    private void OnGenerateClick(object? sender, RoutedEventArgs e) => Guarded(() =>
    {
        if (DataContext is WorkspaceViewModel workspace)
        {
            return ChapterGenerationRunner.RunAsync(this, workspace);
        }

        return Task.CompletedTask;
    });

    private void OnAddChapterClick(object? sender, RoutedEventArgs e) => Guarded(AddChapterAsync);

    private void OnTranslateChapterClick(object? sender, RoutedEventArgs e) => Guarded(RunTranslateChapterAsync);

    private void OnCompleteBookClick(object? sender, RoutedEventArgs e) => Guarded(RunCompleteBookAsync);

    private void OnTranslateMetadataClick(object? sender, RoutedEventArgs e) => Guarded(RunTranslateMetadataAsync);

    private async Task AddChapterAsync()
    {
        if (DataContext is not WorkspaceViewModel workspace || TopLevel.GetTopLevel(this) is not Window window)
        {
            return;
        }

        var viewModel = new ChapterSetupViewModel(
            "Add chapter",
            isAddMode: true,
            ChapterRole.Auto,
            string.Empty,
            (role, notes, variants, session, progress, cancellationToken) =>
                workspace.SuggestChapterSettingsAsync(null, role, notes, variants, session, progress, cancellationToken));

        var dialog = new ChapterSetupWindow { DataContext = viewModel };
        if (await dialog.ShowDialog<bool>(window) && viewModel.SelectedOption is { } option)
        {
            workspace.CreateChapter(viewModel.Role, viewModel.Notes, option.Title, option.Direction);
        }
    }

    private async Task RunCompleteBookAsync()
    {
        if (DataContext is not WorkspaceViewModel workspace || TopLevel.GetTopLevel(this) is not Window window)
        {
            return;
        }

        if (!workspace.HasChapters)
        {
            ErrorDialog.Show(window, "Nothing to do", "There are no chapters yet. Add one with +.");
            return;
        }

        var viewModel = new BookCompletionViewModel(workspace.BuildCompletionPlan, workspace.CompleteBookAsync);
        var dialog = new BookCompletionWindow { DataContext = viewModel };
        await dialog.ShowDialog(window);
    }

    private async Task RunTranslateChapterAsync()
    {
        if (DataContext is not WorkspaceViewModel workspace || TopLevel.GetTopLevel(this) is not Window window)
        {
            return;
        }

        if (!workspace.PrepareTranslation())
        {
            return;
        }

        var viewModel = new ProgressTaskViewModel(
            "Translate chapter",
            "Translates this chapter into every target language of the project.",
            workspace.BuildTranslatePlan(),
            workspace.TranslateChapterAsync);

        await ShowProgressAsync(window, viewModel);
    }

    private static async Task ShowProgressAsync(Window window, ProgressTaskViewModel viewModel)
    {
        var dialog = new ProgressWindow { DataContext = viewModel };
        await dialog.ShowDialog(window);
    }

    private async Task RunTranslateMetadataAsync()
    {
        if (DataContext is not WorkspaceViewModel workspace || TopLevel.GetTopLevel(this) is not Window window)
        {
            return;
        }

        var viewModel = new MetadataTranslationViewModel(workspace.MetadataLanguages, workspace.NeedsMetadataTranslation, workspace.TranslateMetadataAsync);
        var dialog = new MetadataTranslationWindow { DataContext = viewModel };
        await dialog.ShowDialog(window);
    }

    private void Guarded(Func<Task> action)
    {
        _ = GuardedAsync(action);
    }

    private async Task GuardedAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void Execute(Action<WorkspaceViewModel> action)
    {
        if (DataContext is not WorkspaceViewModel workspace)
        {
            return;
        }

        try
        {
            action(workspace);
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void ShowError(Exception exception)
    {
        AppDiagnostics.Write(exception);

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            ErrorDialog.Show(owner, "Something went wrong", exception.Message);
        }
    }

    private static ChapterViewModel? Chapter(object? sender) => (sender as Control)?.DataContext as ChapterViewModel;
}
