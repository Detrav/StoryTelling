using StoryTelling.Domain;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class KnowledgeImportViewModelTests
{
    [Fact]
    public void Run_PopulatesEntriesAndResult()
    {
        var entry = new KnowledgeEntry { Kind = KnowledgeKind.Place, Title = "Ashen Reach", Content = "Frozen." };
        var viewModel = new KnowledgeImportViewModel((_, _, _) => Task.FromResult<IReadOnlyList<KnowledgeEntry>>([entry]));

        Assert.Single(viewModel.Entries);
        Assert.True(viewModel.Entries[0].IsSelected);
        Assert.Single(viewModel.Result!);

        viewModel.Entries[0].IsSelected = false;

        Assert.Empty(viewModel.Result!);
    }
}
