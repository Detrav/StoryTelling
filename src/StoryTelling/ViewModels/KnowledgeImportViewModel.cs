using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Knowledge;
using StoryTelling.Application.Llm;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class KnowledgeImportViewModel : ViewModelBase
{
    public delegate Task<IReadOnlyList<KnowledgeEntry>> Extract(string brief, IProgress<KnowledgeImportProgress>? progress, CancellationToken cancellationToken);

    private readonly Extract _extract;
    private CancellationTokenSource? _cts;

    public KnowledgeImportViewModel(Extract extract)
    {
        _extract = extract;
        Initialization = RunAsync();
    }

    public Task Initialization { get; }

    public ObservableCollection<KnowledgeImportItemViewModel> Entries { get; } = [];

    [ObservableProperty]
    private string _brief = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = string.Empty;

    public IReadOnlyList<KnowledgeEntryEditorViewModel>? Result =>
        [.. Entries.Where(entry => entry.IsSelected).Select(entry => entry.Entry)];

    [RelayCommand]
    private Task Generate() => RunAsync();

    [RelayCommand]
    private void Stop() => Cancel();

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
        Status = "Analyzing…";
        Entries.Clear();

        var progress = new Progress<KnowledgeImportProgress>(report =>
            Status = $"Importing chunk {report.Done} of {report.Total}…");

        try
        {
            var entries = await _extract(Brief, progress, token);
            foreach (var entry in entries)
            {
                Entries.Add(new KnowledgeImportItemViewModel(entry));
            }

            Status = entries.Count == 0
                ? "Nothing was extracted."
                : $"{entries.Count} entries found — uncheck what you do not want, then Apply.";
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped.";
        }
        catch (LlmException exception)
        {
            Status = $"Failed ({exception.Kind}): {exception.Message}";
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
