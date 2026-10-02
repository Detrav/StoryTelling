using CommunityToolkit.Mvvm.ComponentModel;

namespace StoryTelling.ViewModels;

public partial class RoleTemperatureViewModel : ObservableObject
{
    public RoleTemperatureViewModel(string id, string label, double value)
    {
        Id = id;
        Label = label;
        _value = value;
    }

    public string Id { get; }

    public string Label { get; }

    [ObservableProperty]
    private double _value;

    public bool IsLow => Value <= 0.1;

    partial void OnValueChanged(double value) => OnPropertyChanged(nameof(IsLow));
}
