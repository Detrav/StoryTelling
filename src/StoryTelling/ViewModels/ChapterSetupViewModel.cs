using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class ChapterSetupViewModel : ViewModelBase
{
    public delegate Task<IReadOnlyList<GenerationOption>> SuggestOptions(
        ChapterRole role,
        string notes,
        int variants,
        GenerationSession session,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken);

    public const int SuggestionCount = 3;

    private readonly SuggestOptions _suggest;
    private readonly bool _isAddMode;
    private CancellationTokenSource? _cts;

    public ChapterSetupViewModel(string header, bool isAddMode, ChapterRole role, string notes, SuggestOptions suggest)
    {
        Header = header;
        _isAddMode = isAddMode;
        _suggest = suggest;
        _role = role;
        _notes = notes ?? string.Empty;
        _ = LoadAsync();
    }

    public string Header { get; }

    public IReadOnlyList<ChapterRole> Roles { get; } = Enum.GetValues<ChapterRole>();

    public string Description => "Set the role and notes, then pick one of the AI's proposed titles and directions. "
        + "The suggestions use the previous chapters, the knowledge base and the tools.";

    public string ApplyLabel => _isAddMode ? "Add chapter" : "Apply";

    public ObservableCollection<ChapterSetupOptionViewModel> Options { get; } = [];

    [ObservableProperty]
    private ChapterRole _role;

    [ObservableProperty]
    private string _notes;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ChapterSetupOptionViewModel? _selectedOption;

    public bool HasSelection => SelectedOption is not null;

    [RelayCommand]
    private Task Suggest() => LoadAsync();

    [RelayCommand]
    private void Stop() => _cts?.Cancel();

    public void Cancel() => _cts?.Cancel();

    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsBusy = true;
        Status = "Suggesting chapter titles and directions…";
        Options.Clear();
        SelectedOption = null;

        var progress = new Progress<GenerationProgress>(report =>
            Status = report.ToolCalls > 0 ? $"{report.Stage}… ({report.ToolCalls} tool calls)" : $"{report.Stage}…");

        try
        {
            var options = await _suggest(Role, Notes, SuggestionCount, new GenerationSession(), progress, token);
            foreach (var option in options)
            {
                var title = option.Fields.TryGetValue("Title", out var value) ? value.Trim() : string.Empty;
                var direction = option.Fields.TryGetValue("Direction", out var directionValue) ? directionValue.Trim() : string.Empty;
                if (title.Length == 0 && direction.Length == 0)
                {
                    continue;
                }

                Options.Add(new ChapterSetupOptionViewModel(title, direction));
            }

            SelectedOption = Options.FirstOrDefault();
            Status = Options.Count == 0
                ? "No suggestions were returned. Edit the notes and try again."
                : $"{Options.Count} suggestion(s).";
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
