using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;

namespace StoryTelling.ViewModels;

public partial class AiWizardViewModel : ViewModelBase
{
    public delegate Task<IReadOnlyList<GenerationOption>> GenerateOptions(string brief, int options, GenerationSession session, IProgress<GenerationProgress>? progress, CancellationToken cancellationToken);

    private readonly GenerateOptions _generate;
    private readonly IReadOnlyList<GenerationFieldSpec> _specs;
    private readonly string? _editableField;
    private CancellationTokenSource? _cts;
    private GenerationSession _session = new();

    public AiWizardViewModel(string label, GenerationTarget target, GenerateOptions generate, string initialBrief = "")
    {
        Header = $"Generate with AI — {label}";
        _generate = generate;

        _specs = GenerationTargets.Fields(target);
        _editableField = _specs.Count == 1 ? _specs[0].Field : null;
        _optionCount = Math.Clamp(LastOptionCount, 1, 10);
        _brief = initialBrief;

        Initialization = LoadAsync();
    }

    public static int LastOptionCount { get; set; } = 3;

    public Task Initialization { get; }

    public string Header { get; }

    public bool AllowEdits => _editableField is not null;

    public ObservableCollection<GenerationOptionViewModel> Options { get; } = [];

    [ObservableProperty]
    private int _optionCount;

    [ObservableProperty]
    private string _brief = string.Empty;

    [ObservableProperty]
    private string _edits = string.Empty;

    [ObservableProperty]
    private GenerationOptionViewModel? _selectedOption;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = string.Empty;

    public IReadOnlyDictionary<string, string>? Result
    {
        get
        {
            if (_editableField is not null && !string.IsNullOrWhiteSpace(Edits))
            {
                return new Dictionary<string, string> { [_editableField] = Edits.Trim() };
            }

            return SelectedOption?.Fields;
        }
    }

    partial void OnOptionCountChanged(int value) => LastOptionCount = Math.Clamp(value, 1, 10);

    [RelayCommand]
    private Task MoreOptions() => LoadAsync();

    [RelayCommand]
    private void Stop() => Cancel();

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
        Status = "Generating…";

        if (!string.Equals(_session.Brief, Brief, StringComparison.Ordinal))
        {
            _session = new GenerationSession();
        }

        var progress = new Progress<GenerationProgress>(report =>
            Status = report.ToolCalls > 0 ? $"{report.Stage}… ({report.ToolCalls} tool calls)" : $"{report.Stage}…");

        try
        {
            var options = await _generate(Brief, OptionCount, _session, progress, token);
            foreach (var option in options)
            {
                Options.Add(new GenerationOptionViewModel(Format(option), option.Fields));
            }

            SelectedOption ??= Options.FirstOrDefault();
            Status = options.Count == 0
                ? "No options were returned."
                : $"{options.Count} options · {_session.ToolCalls} tool calls";
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

    private string Format(GenerationOption option)
    {
        var present = _specs.Where(spec => option.Fields.ContainsKey(spec.Field)).ToList();
        if (present.Count <= 2)
        {
            return string.Join("\n\n", present.Select(spec => option.Fields[spec.Field]));
        }

        return string.Join("\n", present.Select(spec => $"{spec.Label}: {option.Fields[spec.Field]}"));
    }
}
