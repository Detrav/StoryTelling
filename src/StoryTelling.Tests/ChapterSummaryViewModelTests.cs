using StoryTelling.Domain;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class ChapterSummaryViewModelTests
{
    [Fact]
    public void AddEditRemove_SyncsToChapter()
    {
        var chapter = new ChapterViewModel();
        var viewModel = new ChapterSummaryViewModel(chapter, () => { });

        var change = new KnowledgeChangeEditorViewModel
        {
            Operation = KnowledgeChangeOperation.Create,
            Kind = KnowledgeKind.Character,
            Title = "Aria",
            Reason = "Introduced.",
        };

        viewModel.AddChange(change);

        Assert.Same(change, viewModel.SelectedChange);
        var stored = Assert.Single(chapter.KnowledgeChanges);
        Assert.Equal("Aria", stored.Title);
        Assert.Equal(KnowledgeChangeOperation.Create, stored.Operation);

        var draft = change.Clone();
        draft.Content = "Updated.";
        viewModel.ApplyChangeEdit(change, draft);
        Assert.Equal("Updated.", chapter.KnowledgeChanges[0].Content);

        viewModel.RemoveChange(change);
        Assert.Empty(chapter.KnowledgeChanges);
        Assert.Empty(viewModel.Changes);
    }

    [Fact]
    public void ChapterKnowledgeChanges_RefreshCollection()
    {
        var chapter = new ChapterViewModel();
        var viewModel = new ChapterSummaryViewModel(chapter, () => { });

        chapter.KnowledgeChanges = [new KnowledgeChange { Operation = KnowledgeChangeOperation.Delete, Title = "Old Map" }];

        var change = Assert.Single(viewModel.Changes);
        Assert.Equal(KnowledgeChangeOperation.Delete, change.Operation);
        Assert.Equal("Old Map", change.Title);
    }
}
