using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StoryTelling.ViewModels;

public partial class SettingsWindowViewModel : ViewModelBase
{
    public SettingsWindowViewModel(LanguageCatalog catalog)
    {
        Languages = catalog.Items;
        _selectedDefault = Languages.FirstOrDefault(language => language.Code == catalog.DefaultCode)
            ?? Languages.FirstOrDefault();
    }

    public ObservableCollection<LanguageOption> Languages { get; }

    public ObservableCollection<string> Providers { get; } =
    [
        "OpenAI",
        "OpenRouter",
        "Ollama",
        "LM Studio",
    ];

    [ObservableProperty]
    private string _provider = "OpenAI";

    [ObservableProperty]
    private string _baseUrl = "https://api.openai.com/v1";

    [ObservableProperty]
    private string _model = "gpt-4o-mini";

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private int _timeoutSeconds = 120;

    [ObservableProperty]
    private int _maxTokens = 2048;

    [ObservableProperty]
    private double _temperature = 0.8;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _newLanguageCode = string.Empty;

    [ObservableProperty]
    private string _newLanguageName = string.Empty;

    [ObservableProperty]
    private LanguageOption? _selectedDefault;

    partial void OnProviderChanged(string value)
    {
        BaseUrl = value switch
        {
            "OpenRouter" => "https://openrouter.ai/api/v1",
            "Ollama" => "http://localhost:11434/v1",
            "LM Studio" => "http://localhost:1234/v1",
            _ => "https://api.openai.com/v1",
        };
    }

    [RelayCommand]
    private void AddLanguage()
    {
        var code = NewLanguageCode.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(code))
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(NewLanguageName) ? code.ToUpperInvariant() : NewLanguageName.Trim();
        Languages.Add(new LanguageOption(code, name));
        NewLanguageCode = string.Empty;
        NewLanguageName = string.Empty;
    }

    [RelayCommand]
    private void RemoveLanguage(LanguageOption? option)
    {
        if (option is not null)
        {
            Languages.Remove(option);
        }
    }

    [RelayCommand]
    private void TestConnection() => Status = "Mock: connection OK";
}
