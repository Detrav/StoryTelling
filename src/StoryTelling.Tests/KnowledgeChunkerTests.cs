using StoryTelling.Application.Retrieval;

namespace StoryTelling.Tests;

public sealed class KnowledgeChunkerTests
{
    [Fact]
    public void Split_MergesSmallParagraphs()
    {
        var chunks = KnowledgeChunker.Split("One.\n\nTwo.\n\nThree.", maxChars: 50);

        Assert.Single(chunks);
        Assert.Contains("One.", chunks[0]);
        Assert.Contains("Three.", chunks[0]);
    }

    [Fact]
    public void Split_BreaksLongParagraph()
    {
        var chunks = KnowledgeChunker.Split(new string('a', 250), maxChars: 100);

        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, chunk => Assert.True(chunk.Length <= 100));
    }

    [Fact]
    public void Split_SeparatesLargeParagraphs()
    {
        var text = $"{new string('a', 80)}\n\n{new string('b', 80)}";

        var chunks = KnowledgeChunker.Split(text, maxChars: 100);

        Assert.Equal(2, chunks.Count);
    }

    [Fact]
    public void Split_BlankContent_ReturnsEmpty() => Assert.Empty(KnowledgeChunker.Split("   "));

    [Fact]
    public void Split_MergesSmallSections()
    {
        const string content = "# Region\nA frozen frontier.\n\n## Keep\nKethral Keep fell at dusk.\n\n## Vault\nThe Ember Vault is hidden.";

        var chunks = KnowledgeChunker.Split(content, maxChars: 500);

        var chunk = Assert.Single(chunks);
        Assert.Contains("# Region", chunk);
        Assert.Contains("## Keep", chunk);
        Assert.Contains("## Vault", chunk);
    }

    [Fact]
    public void Split_KeepsSectionsSeparate_WhenTheyDoNotFit()
    {
        const string content =
            "# Region\nA frozen frontier of ash and ruined keeps lay beyond the wall.\n\n"
            + "## Keep\nKethral Keep fell at dusk and no one knows why it happened.\n\n"
            + "## Vault\nThe Ember Vault is hidden and guarded by dead wards.";

        var chunks = KnowledgeChunker.Split(content, maxChars: 100);

        Assert.Equal(3, chunks.Count);
        Assert.StartsWith("# Region", chunks[0]);
        Assert.StartsWith("## Keep", chunks[1]);
        Assert.StartsWith("## Vault", chunks[2]);
    }

    [Fact]
    public void Split_LargeSection_SplitsButKeepsHeadingFirst()
    {
        var content = $"# Section\n\n{new string('a', 250)}";

        var chunks = KnowledgeChunker.Split(content, maxChars: 100);

        Assert.True(chunks.Count >= 2);
        Assert.StartsWith("# Section", chunks[0]);
    }

    [Fact]
    public void Split_WithoutHeadings_StillChunksByParagraphs()
    {
        var chunks = KnowledgeChunker.Split("One.\n\nTwo.", maxChars: 50);

        Assert.Single(chunks);
    }
}
