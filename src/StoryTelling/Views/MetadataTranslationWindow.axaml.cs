using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using StoryTelling.ViewModels;

namespace StoryTelling.Views;

public partial class MetadataTranslationWindow : Window
{
    private MetadataTranslationViewModel? _viewModel;

    public MetadataTranslationWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
        Closing += OnClosing;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Detach();
        _viewModel = DataContext as MetadataTranslationViewModel;

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

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void CloseFromViewModel() => Close();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) => ScrollToRunning();

    private void ScrollToRunning()
    {
        if (_viewModel is null)
        {
            return;
        }

        for (var index = 0; index < _viewModel.Languages.Count; index++)
        {
            if (_viewModel.Languages[index].IsRunning)
            {
                LanguageList.ScrollIntoView(index);
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
