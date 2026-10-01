using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class FixTargetPickerWindow : Window
{
    public FixTargetPickerWindow() => InitializeComponent();

    private void OnContinueClick(object? sender, RoutedEventArgs e) =>
        Close((DataContext as FixTargetPickerViewModel)?.Selected);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
