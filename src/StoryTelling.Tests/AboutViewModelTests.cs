using System.Reflection;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class AboutViewModelTests
{
    [Fact]
    public void Version_IsTakenFromTheAssembly()
    {
        var viewModel = new AboutViewModel();

        var expected = typeof(AboutViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var plus = expected?.IndexOf('+') ?? -1;
        var stripped = plus < 0 ? expected : expected?[..plus];

        Assert.False(string.IsNullOrWhiteSpace(viewModel.Version));
        Assert.Equal(stripped, viewModel.Version);
        Assert.DoesNotContain('+', viewModel.Version);
    }

    [Fact]
    public void Copyright_IsTakenFromTheAssembly()
    {
        var viewModel = new AboutViewModel();

        Assert.False(string.IsNullOrWhiteSpace(viewModel.Copyright));
    }

    [Fact]
    public void DefaultChoice_MustRemainCancel_SoClosingTheDialogNeverDiscards()
    {
        Assert.Equal(UnsavedChangesChoice.Cancel, default(UnsavedChangesChoice));
    }

    [Fact]
    public void UnsavedChangesMessage_NamesTheProjectAndOffersThreeAnswers()
    {
        var viewModel = new UnsavedChangesViewModel("The Ember Crown");

        Assert.Equal("The Ember Crown", viewModel.ProjectName);
        Assert.Contains("The Ember Crown", viewModel.Message);
        Assert.Contains("Save", viewModel.Message);
        Assert.Equal("Unsaved changes", viewModel.Title);
    }
}
