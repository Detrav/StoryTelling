using StoryTelling.Application.Knowledge;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

internal sealed class FakeKnowledgeImporter : IKnowledgeImporter
{
    public IReadOnlyList<KnowledgeEntry> Entries { get; set; } = [];

    public KnowledgeImportRequest? LastRequest { get; private set; }

    public Task<IReadOnlyList<KnowledgeEntry>> ExtractAsync(
        KnowledgeImportRequest request,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        progress?.Report(1);
        return Task.FromResult(Entries);
    }
}
