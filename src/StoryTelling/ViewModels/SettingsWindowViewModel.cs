using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Settings;

namespace StoryTelling.ViewModels;

public partial class SettingsWindowViewModel : ViewModelBase
{
    private readonly AppSettings _original;

    public SettingsWindowViewModel(AppSettings settings)
    {
        _original = settings;
        Languages = new ObservableCollection<LanguageData>(settings.Languages);
        _provider = settings.Provider;
        _baseUrl = settings.BaseUrl;
        _model = settings.Model;
        _apiKey = settings.ApiKey;
        _timeoutSeconds = settings.TimeoutSeconds;
        _maxTokens = settings.MaxTokens;
        _temperature = settings.Temperature;
        _selectedDefault = Languages.FirstOrDefault(language => language.Code == settings.DefaultLanguageCode)
            ?? Languages.FirstOrDefault();
    }

    public ObservableCollection<LanguageData> Languages { get; }

    public ObservableCollection<string> Providers { get; } =
    [
        "OpenAI",
        "OpenRouter",
        "Ollama",
        "LM Studio",
    ];

    [ObservableProperty]
    private string _provider;

    [ObservableProperty]
    private string _baseUrl;

    [ObservableProperty]
    private string _model;

    [ObservableProperty]
    private string _apiKey;

    [ObservableProperty]
    private int _timeoutSeconds;

    [ObservableProperty]
    private int _maxTokens;

    [ObservableProperty]
    private double _temperature;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _newLanguageCode = string.Empty;

    [ObservableProperty]
    private string _newLanguageName = string.Empty;

    [ObservableProperty]
    private LanguageData? _selectedDefault;

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
        if (Languages.All(language => language.Code != code))
        {
            Languages.Add(new LanguageData(code, name));
        }

        NewLanguageCode = string.Empty;
        NewLanguageName = string.Empty;
    }

    [RelayCommand]
    private void RemoveLanguage(LanguageData? language)
    {
        if (language is not null)
        {
            Languages.Remove(language);
        }
    }

    [RelayCommand]
    private void TestConnection() => Status = "Mock: connection OK";

    public AppSettings BuildSettings() => new()
    {
        SchemaVersion = AppSettings.CurrentSchemaVersion,
        Provider = Provider,
        BaseUrl = BaseUrl,
        Model = Model,
        ApiKey = ApiKey,
        TimeoutSeconds = TimeoutSeconds,
        MaxTokens = MaxTokens,
        Temperature = Temperature,
        DefaultLanguageCode = SelectedDefault?.Code ?? _original.DefaultLanguageCode,
        Languages = Languages.ToList(),
        RecentProjects = _original.RecentProjects.ToList(),
    };
}
