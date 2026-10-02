using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Settings;

namespace StoryTelling.ViewModels;

public partial class SettingsWindowViewModel : UndoableDialogViewModel
{
    private readonly AppSettings _original;
    private readonly ILlmClient _llmClient;

    public SettingsWindowViewModel(ITextDiff diff, AppSettings settings, ILlmClient llmClient)
        : base(diff)
    {
        _original = settings;
        _llmClient = llmClient;
        Languages = new ObservableCollection<LanguageData>(settings.Languages);
        _provider = settings.Provider;
        _baseUrl = settings.BaseUrl;
        _model = settings.Model;
        _apiKey = settings.ApiKey;
        _timeoutSeconds = settings.TimeoutSeconds;
        _maxTokens = settings.MaxTokens;
        _maxToolCalls = settings.MaxToolCalls;
        _contextTokenBudget = settings.ContextTokenBudget;
        _recentLoglineCount = settings.RecentLoglineCount;
        _contextRequiredSectionMaxChars = settings.ContextRequiredSectionMaxChars;
        _toolResultMaxChars = settings.ToolResultMaxChars;
        _temperature = settings.Temperature;
        _editorStageCount = settings.EditorStageCount;
        _selectedDefault = Languages.FirstOrDefault(language => language.Code == settings.DefaultLanguageCode)
            ?? Languages.FirstOrDefault();

        RoleTemperatures = new ObservableCollection<RoleTemperatureViewModel>(
            LlmTasks.All.Select(task => new RoleTemperatureViewModel(task.Id(), task.Label(), settings.TemperatureFor(task))));

        InitializeUndo();
    }

    public ObservableCollection<LanguageData> Languages { get; }

    public ObservableCollection<RoleTemperatureViewModel> RoleTemperatures { get; }

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
    private int _maxToolCalls;

    [ObservableProperty]
    private int _contextTokenBudget;

    [ObservableProperty]
    private int _recentLoglineCount;

    [ObservableProperty]
    private int _contextRequiredSectionMaxChars;

    [ObservableProperty]
    private int _toolResultMaxChars;

    [ObservableProperty]
    private double _temperature;

    [ObservableProperty]
    private int _editorStageCount;

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
        Commit();
    }

    [RelayCommand]
    private void RemoveLanguage(LanguageData? language)
    {
        if (language is not null)
        {
            Languages.Remove(language);
            Commit();
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        Status = "Testing connection…";

        try
        {
            var connection = LlmConnection.From(BaseUrl, ApiKey, TimeoutSeconds);
            var request = new LlmRequest
            {
                Model = Model,
                Messages = [LlmMessage.User("Reply with the single word OK.")],
                Temperature = 0,
                MaxTokens = 16,
            };

            var completion = await _llmClient.CompleteAsync(connection, request);
            var reply = completion.Content.Trim();
            var chat = string.IsNullOrEmpty(reply) ? "chat OK" : $"chat OK ({reply})";

            var structured = await _llmClient.CheckStructuredOutputAsync(connection, Model);
            Status = structured.Supported
                ? $"Connection OK · {chat} · structured output: supported"
                : $"{chat} · structured output NOT supported: {structured.Detail}. "
                  + "Enable JSON-schema structured output in your provider or pick a model that supports it.";
        }
        catch (LlmException exception)
        {
            Status = $"Failed ({exception.Kind}): {exception.Message}";
        }
        catch (Exception exception)
        {
            Status = $"Failed: {exception.Message}";
        }
    }

    public AppSettings BuildSettings() => new()
    {
        SchemaVersion = AppSettings.CurrentSchemaVersion,
        Provider = Provider,
        BaseUrl = BaseUrl,
        Model = Model,
        ApiKey = ApiKey,
        TimeoutSeconds = TimeoutSeconds,
        MaxTokens = MaxTokens,
        MaxToolCalls = MaxToolCalls,
        ContextTokenBudget = ContextTokenBudget,
        RecentLoglineCount = RecentLoglineCount,
        ContextRequiredSectionMaxChars = ContextRequiredSectionMaxChars,
        ToolResultMaxChars = ToolResultMaxChars,
        Temperature = Temperature,
        EditorStageCount = EditorStageCount,
        RoleTemperatures = RoleTemperatures.ToDictionary(item => item.Id, item => item.Value),
        DefaultLanguageCode = SelectedDefault?.Code ?? _original.DefaultLanguageCode,
        Languages = Languages.ToList(),
        RecentProjects = _original.RecentProjects.ToList(),
    };

    protected override string CaptureState()
    {
        var snapshot = new SettingsSnapshot(
            Provider,
            BaseUrl,
            Model,
            ApiKey,
            TimeoutSeconds,
            MaxTokens,
            MaxToolCalls,
            ContextTokenBudget,
            RecentLoglineCount,
            ContextRequiredSectionMaxChars,
            ToolResultMaxChars,
            Temperature,
            EditorStageCount,
            RoleTemperatures.ToDictionary(item => item.Id, item => item.Value),
            SelectedDefault?.Code ?? _original.DefaultLanguageCode,
            [.. Languages]);

        return JsonSerializer.Serialize(snapshot);
    }

    protected override void ApplyState(string state)
    {
        var snapshot = JsonSerializer.Deserialize<SettingsSnapshot>(state);
        if (snapshot is null)
        {
            return;
        }

        Provider = snapshot.Provider;
        BaseUrl = snapshot.BaseUrl;
        Model = snapshot.Model;
        ApiKey = snapshot.ApiKey;
        TimeoutSeconds = snapshot.TimeoutSeconds;
        MaxTokens = snapshot.MaxTokens;
        MaxToolCalls = snapshot.MaxToolCalls;
        ContextTokenBudget = snapshot.ContextTokenBudget;
        RecentLoglineCount = snapshot.RecentLoglineCount;
        ContextRequiredSectionMaxChars = snapshot.ContextRequiredSectionMaxChars;
        ToolResultMaxChars = snapshot.ToolResultMaxChars;
        Temperature = snapshot.Temperature;
        EditorStageCount = snapshot.EditorStageCount;

        foreach (var item in RoleTemperatures)
        {
            if (snapshot.RoleTemperatures.TryGetValue(item.Id, out var value))
            {
                item.Value = value;
            }
        }

        Languages.Clear();
        foreach (var language in snapshot.Languages)
        {
            Languages.Add(language);
        }

        SelectedDefault = Languages.FirstOrDefault(language => language.Code == snapshot.DefaultLanguageCode)
            ?? Languages.FirstOrDefault();
    }

    private sealed record SettingsSnapshot(
        string Provider,
        string BaseUrl,
        string Model,
        string ApiKey,
        int TimeoutSeconds,
        int MaxTokens,
        int MaxToolCalls,
        int ContextTokenBudget,
        int RecentLoglineCount,
        int ContextRequiredSectionMaxChars,
        int ToolResultMaxChars,
        double Temperature,
        int EditorStageCount,
        Dictionary<string, double> RoleTemperatures,
        string DefaultLanguageCode,
        List<LanguageData> Languages);
}
