using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Application.Llm;

namespace StoryTelling.ViewModels;

public partial class RoleTemperatureViewModel : ObservableObject
{
    public const string ProviderDefault = "provider default";

    public static IReadOnlyList<string> EffortOptions { get; } = [ProviderDefault, .. LlmTasks.ReasoningEffortLevels];

    public RoleTemperatureViewModel(string id, string label, double value, string effort)
    {
        Id = id;
        Label = label;
        _value = value;
        _effort = ToDisplay(effort);
    }

    public string Id { get; }

    public string Label { get; }

    public IReadOnlyList<string> EffortOptionsList => EffortOptions;

    [ObservableProperty]
    private double _value;

    [ObservableProperty]
    private string _effort;

    public string EffortValue =>
        string.Equals(Effort, ProviderDefault, StringComparison.OrdinalIgnoreCase) ? string.Empty : Effort;

    public bool IsLow => Value <= 0.1;

    partial void OnValueChanged(double value) => OnPropertyChanged(nameof(IsLow));

    private static string ToDisplay(string effort) =>
        string.IsNullOrWhiteSpace(effort) ? ProviderDefault : effort.Trim();
}
