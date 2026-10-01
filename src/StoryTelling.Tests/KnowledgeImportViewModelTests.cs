using StoryTelling.Domain;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class KnowledgeImportViewModelTests
{
    [Fact]
    public void Run_PopulatesEntriesAndResult()
    {
        var entry = new KnowledgeEntry { Kind = KnowledgeKind.Place, Title = "Ashen Reach", Content = "Frozen." };
        var viewModel = new KnowledgeImportViewModel((_, _, _, _) => Task.FromResult<IReadOnlyList<KnowledgeEntry>>([entry]), initialSource: "some source text");

        Assert.Single(viewModel.Entries);
        Assert.True(viewModel.Entries[0].IsSelected);
        Assert.Single(viewModel.Result!);

        viewModel.Entries[0].IsSelected = false;

        Assert.Empty(viewModel.Result!);
    }

    [Fact]
    public void ExistingTitles_AreUnselected()
    {
        var entry = new KnowledgeEntry { Kind = KnowledgeKind.Place, Title = "Ashen Reach" };
        var viewModel = new KnowledgeImportViewModel(
            (_, _, _, _) => Task.FromResult<IReadOnlyList<KnowledgeEntry>>([entry]),
            initialSource: "source",
            existingTitles: ["Ashen Reach"]);

        Assert.False(Assert.Single(viewModel.Entries).IsSelected);
    }

    [Fact]
    public async Task PromptMode_WaitsForGenerateAndRequiresSource()
    {
        var entry = new KnowledgeEntry { Kind = KnowledgeKind.Character, Title = "Elias" };
        var viewModel = new KnowledgeImportViewModel(
            (_, _, _, _) => Task.FromResult<IReadOnlyList<KnowledgeEntry>>([entry]),
            autoRun: false,
            showSource: true,
            header: "Knowledge from prompt");

        Assert.True(viewModel.ShowSource);
        Assert.Equal("Knowledge from prompt", viewModel.Header);
        Assert.Empty(viewModel.Entries);

        await viewModel.GenerateCommand.ExecuteAsync(null);
        Assert.Empty(viewModel.Entries);
        Assert.Contains("Describe", viewModel.Status);

        viewModel.Source = "A drowned port city.";
        await viewModel.GenerateCommand.ExecuteAsync(null);
        Assert.Single(viewModel.Entries);
    }
}
