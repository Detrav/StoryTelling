using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;

namespace StoryTelling.ViewModels;

public partial class AiWizardViewModel : ViewModelBase
{
    public delegate Task<IReadOnlyList<GenerationOption>> GenerateOptions(string brief, CancellationToken cancellationToken);

    private readonly GenerateOptions _generate;
    private readonly IReadOnlyList<string> _fieldOrder;
    private readonly string? _editableField;

    public AiWizardViewModel(string label, GenerationTarget target, GenerateOptions generate)
    {
        Header = $"Generate with AI — {label}";
        _generate = generate;

        var fields = GenerationTargets.Fields(target);
        _fieldOrder = fields.Select(spec => spec.Field).ToList();
        _editableField = fields.Count == 1 ? fields[0].Field : null;

        _ = LoadAsync();
    }

    public string Header { get; }

    public bool AllowEdits => _editableField is not null;

    public ObservableCollection<GenerationOptionViewModel> Options { get; } = [];

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

    [RelayCommand]
    private Task MoreOptions() => LoadAsync();

    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Status = "Generating…";
        try
        {
            var options = await _generate(Brief, CancellationToken.None);
            Options.Clear();
            foreach (var option in options)
            {
                Options.Add(new GenerationOptionViewModel(Format(option), option.Fields));
            }

            SelectedOption = Options.FirstOrDefault();
            Status = Options.Count == 0 ? "No options were returned." : string.Empty;
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

    private string Format(GenerationOption option) =>
        string.Join("\n\n", _fieldOrder.Where(field => option.Fields.ContainsKey(field)).Select(field => option.Fields[field]));
}
