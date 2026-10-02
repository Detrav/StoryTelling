using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class ChapterSummaryViewModel : ViewModelBase
{
    private readonly ChapterViewModel _chapter;
    private readonly CommitDebouncer _debouncer;
    private bool _syncing;
    private CancellationTokenSource? _cts;

    public ChapterSummaryViewModel(ChapterViewModel chapter, Action commit)
    {
        _chapter = chapter;
        _debouncer = new CommitDebouncer(commit, TimeSpan.FromMilliseconds(700));
        chapter.PropertyChanged += OnChapterChanged;
        RefreshChanges();
    }

    public Func<IProgress<GenerationProgress>?, CancellationToken, Task>? Regenerate { get; set; }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = string.Empty;

    public ObservableCollection<KnowledgeChangeEditorViewModel> Changes { get; } = [];

    public ObservableCollection<ProgressItemViewModel> Steps { get; } =
        [new ProgressItemViewModel("Summarize and update the story state")];

    public IReadOnlyList<EditorNote> EditorNotes => _chapter.EditorNotes;

    [ObservableProperty]
    private KnowledgeChangeEditorViewModel? _selectedChange;

    public string Logline
    {
        get => _chapter.Logline;
        set
        {
            if (_chapter.Logline == value)
            {
                return;
            }

            _chapter.Logline = value;
            _debouncer.Trigger();
        }
    }

    public string StorySoFar
    {
        get => _chapter.StorySoFar;
        set
        {
            if (_chapter.StorySoFar == value)
            {
                return;
            }

            _chapter.StorySoFar = value;
            _debouncer.Trigger();
        }
    }

    public string TimeAndPlace
    {
        get => _chapter.WorldState?.TimeAndPlace ?? string.Empty;
        set
        {
            var state = EnsureState();
            if (state.TimeAndPlace == value)
            {
                return;
            }

            state.TimeAndPlace = value;
            _debouncer.Trigger();
        }
    }

    public string StateDescription
    {
        get => _chapter.WorldState?.Description ?? string.Empty;
        set
        {
            var state = EnsureState();
            if (state.Description == value)
            {
                return;
            }

            state.Description = value;
            _debouncer.Trigger();
        }
    }

    public void AddChange(KnowledgeChangeEditorViewModel change)
    {
        Changes.Add(change);
        SelectedChange = change;
        CommitChanges();
    }

    public void ApplyChangeEdit(KnowledgeChangeEditorViewModel target, KnowledgeChangeEditorViewModel draft)
    {
        target.CopyFrom(draft);
        CommitChanges();
    }

    public void RemoveChange(KnowledgeChangeEditorViewModel change)
    {
        Changes.Remove(change);
        if (ReferenceEquals(SelectedChange, change))
        {
            SelectedChange = null;
        }

        CommitChanges();
    }

    public void Commit() => _debouncer.CommitNow();

    [RelayCommand]
    private async Task RegenerateSummary()
    {
        if (Regenerate is not { } regenerate || IsBusy)
        {
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsBusy = true;
        Status = "Regenerating…";
        ProgressItems.Update(Steps, 0, 0, Status);

        var progress = new Progress<GenerationProgress>(report =>
        {
            Status = report.ToolCalls > 0 ? $"{report.Stage}… ({report.ToolCalls} tool calls)" : $"{report.Stage}…";
            ProgressItems.Update(Steps, 0, 0, Status);
        });

        try
        {
            await regenerate(progress, token);
            Status = "Summary regenerated.";
            ProgressItems.MarkAllDone(Steps);
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped.";
            ProgressItems.CancelRunning(Steps);
        }
        catch (Exception exception)
        {
            Status = $"Failed: {exception.Message}";
            ProgressItems.FailRunning(Steps, exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void StopRegenerate() => _cts?.Cancel();

    private WorldState EnsureState() => _chapter.WorldState ??= new WorldState();

    private void CommitChanges()
    {
        _syncing = true;
        _chapter.KnowledgeChanges = [.. Changes.Select(change => change.ToChange())];
        _syncing = false;
        _debouncer.Trigger();
    }

    private void RefreshChanges()
    {
        if (_syncing)
        {
            return;
        }

        Changes.Clear();
        SelectedChange = null;
        foreach (var change in _chapter.KnowledgeChanges)
        {
            Changes.Add(new KnowledgeChangeEditorViewModel(change));
        }
    }

    private void OnChapterChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ChapterViewModel.Logline):
                OnPropertyChanged(nameof(Logline));
                break;
            case nameof(ChapterViewModel.StorySoFar):
                OnPropertyChanged(nameof(StorySoFar));
                break;
            case nameof(ChapterViewModel.WorldState):
                OnPropertyChanged(nameof(TimeAndPlace));
                OnPropertyChanged(nameof(StateDescription));
                break;
            case nameof(ChapterViewModel.KnowledgeChanges):
                RefreshChanges();
                break;
            case nameof(ChapterViewModel.EditorNotes):
                OnPropertyChanged(nameof(EditorNotes));
                break;
        }
    }
}
