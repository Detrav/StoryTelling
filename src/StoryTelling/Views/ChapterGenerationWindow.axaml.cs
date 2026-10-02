using System.ComponentModel;
using Avalonia.Controls;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class ChapterGenerationWindow : Window
{
    private ChapterGenerationViewModel? _viewModel;

    public ChapterGenerationWindow()
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
        _viewModel = DataContext as ChapterGenerationViewModel;

        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += CloseFromViewModel;
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

    private void CloseFromViewModel() => Close();

    private void Detach()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.CloseRequested -= CloseFromViewModel;
        _viewModel = null;
    }
}
