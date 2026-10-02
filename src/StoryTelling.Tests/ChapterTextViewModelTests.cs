using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class ChapterTextViewModelTests
{
    [Fact]
    public void ReplaceText_AppliesTheGeneratedTextExactlyOnce()
    {
        var applied = new List<string>();
        var viewModel = new ChapterTextViewModel("old text", false, applied.Add, () => { });

        viewModel.ReplaceText("generated text");

        Assert.Equal(["generated text"], applied);
        Assert.Equal("generated text", viewModel.Text);
    }

    [Fact]
    public void ReplaceText_AfterAFailedApply_StillAppliesTheNextEdit()
    {
        var calls = 0;
        var viewModel = new ChapterTextViewModel("old", false, _ =>
        {
            calls++;
            if (calls == 1)
            {
                throw new InvalidOperationException("boom");
            }
        }, () => { });

        Assert.Throws<InvalidOperationException>(() => viewModel.ReplaceText("first"));

        viewModel.ReplaceText("second");

        Assert.Equal("second", viewModel.Text);
        Assert.Equal(2, calls);
    }
}
