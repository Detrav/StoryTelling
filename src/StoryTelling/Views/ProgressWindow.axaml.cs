using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class ProgressWindow : Window
{
    private ProgressTaskViewModel? _viewModel;

    public ProgressWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Detach();
        _viewModel = DataContext as ProgressTaskViewModel;

        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += CloseFromViewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            await _viewModel.StartAsync();
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e) => _viewModel?.CancelWork();

    private void OnClosed(object? sender, EventArgs e) => Detach();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void CloseFromViewModel() => Close();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) => ScrollToRunning();

    private void ScrollToRunning()
    {
        if (_viewModel is null)
        {
            return;
        }

        for (var index = 0; index < _viewModel.Items.Count; index++)
        {
            if (_viewModel.Items[index].IsRunning)
            {
                TaskList.ScrollIntoView(index);
                return;
            }
        }
    }

    private void Detach()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.CloseRequested -= CloseFromViewModel;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = null;
    }
}
