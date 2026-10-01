using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Knowledge;
using StoryTelling.Application.Llm;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class KnowledgeImportViewModel : ViewModelBase
{
    public delegate Task<IReadOnlyList<KnowledgeEntry>> Extract(string source, string brief, IProgress<KnowledgeImportProgress>? progress, CancellationToken cancellationToken);

    private readonly Extract _extract;
    private readonly HashSet<string> _existingTitles;
    private CancellationTokenSource? _cts;

    public KnowledgeImportViewModel(Extract extract, string initialSource = "", bool autoRun = true, bool showSource = false, string header = "Import knowledge", IReadOnlyList<string>? existingTitles = null)
    {
        _extract = extract;
        _source = initialSource;
        Header = header;
        ShowSource = showSource;
        _existingTitles = new HashSet<string>(existingTitles ?? [], StringComparer.OrdinalIgnoreCase);
        Initialization = autoRun ? RunAsync() : Task.CompletedTask;
    }

    public Task Initialization { get; }

    public string Header { get; }

    public bool ShowSource { get; }

    public ObservableCollection<KnowledgeImportItemViewModel> Entries { get; } = [];

    [ObservableProperty]
    private string _source = string.Empty;

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

        if (string.IsNullOrWhiteSpace(Source))
        {
            Status = "Describe what the knowledge base should contain first.";
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
            var entries = await _extract(Source, Brief, progress, token);
            foreach (var entry in entries)
            {
                var item = new KnowledgeImportItemViewModel(entry);
                if (_existingTitles.Contains(entry.Title.Trim()))
                {
                    item.IsSelected = false;
                }

                Entries.Add(item);
            }

            Status = entries.Count == 0
                ? "Nothing was produced."
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
