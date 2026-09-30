using StoryTelling.Domain;
using StoryTelling.ViewModels;

namespace StoryTelling.Tests;

public sealed class ChapterSettingsViewModelTests
{
    [Fact]
    public void Participation_TogglesAreMutuallyExclusiveAndMapToChapter()
    {
        var chapter = new ChapterViewModel { Number = 1 };
        var aria = new Character { Id = Guid.NewGuid(), Name = "Aria" };
        var bran = new Character { Id = Guid.NewGuid(), Name = "Bran" };
        var settings = new ChapterSettingsViewModel(chapter, [aria, bran], () => { });

        var ariaParticipation = settings.Participants.Single(participant => participant.CharacterId == aria.Id);

        ariaParticipation.IsFull = true;
        Assert.True(ariaParticipation.IsFull);
        Assert.False(ariaParticipation.IsNameOnly);

        ariaParticipation.IsNameOnly = true;
        Assert.False(ariaParticipation.IsFull);
        Assert.True(ariaParticipation.IsNameOnly);
        Assert.Contains(chapter.Characters, link => link.CharacterId == aria.Id && link.Presence == CharacterPresence.NameOnly);
        Assert.DoesNotContain(chapter.Characters, link => link.CharacterId == bran.Id);

        ariaParticipation.IsNameOnly = false;
        Assert.DoesNotContain(chapter.Characters, link => link.CharacterId == aria.Id);
    }
}
