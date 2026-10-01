using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Generation;

namespace StoryTelling.ViewModels;

public partial class ChapterPlanViewModel : ViewModelBase
{
    public const int MaxChapters = 50;

    public delegate Task<IReadOnlyList<GenerationOption>> PlanGenerator(int count, string brief, GenerationSession session, IProgress<GenerationProgress>? progress, CancellationToken cancellationToken);

    private readonly PlanGenerator _generate;
    private CancellationTokenSource? _cts;

    public ChapterPlanViewModel(PlanGenerator generate, int initialCount = 10)
    {
        _generate = generate;
        _count = Math.Clamp(initialCount, 1, MaxChapters);
    }

    public int MaximumCount => MaxChapters;

    public ObservableCollection<ChapterPlanItemViewModel> Chapters { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlan))]
    private int _count;

    [ObservableProperty]
    private string _brief = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = "Set the chapter count and press Generate.";

    public bool HasPlan => Chapters.Count > 0;

    [RelayCommand]
    private Task Generate() => RunAsync();

    [RelayCommand]
    private void Stop() => _cts?.Cancel();

    public void Cancel() => _cts?.Cancel();

    private async Task RunAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsBusy = true;
        Status = "Planning…";
        Chapters.Clear();
        OnPropertyChanged(nameof(HasPlan));

        var progress = new Progress<GenerationProgress>(report =>
            Status = report.ToolCalls > 0 ? $"{report.Stage}… ({report.ToolCalls} tool calls)" : $"{report.Stage}…");

        try
        {
            var options = await _generate(Count, Brief, new GenerationSession(), progress, token);
            var number = 1;
            foreach (var option in options)
            {
                var title = option.Fields.TryGetValue("Title", out var t) ? t.Trim() : string.Empty;
                var direction = option.Fields.TryGetValue("Direction", out var d) ? d.Trim() : string.Empty;
                if (title.Length == 0 && direction.Length == 0)
                {
                    continue;
                }

                Chapters.Add(new ChapterPlanItemViewModel(number++, title, direction));
            }

            OnPropertyChanged(nameof(HasPlan));
            Status = Chapters.Count == 0
                ? "No chapters were planned. Try again or adjust the brief."
                : $"{Chapters.Count} chapters planned — review, then Apply.";
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped.";
        }
        catch (Exception exception)
        {
            Status = $"Failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
