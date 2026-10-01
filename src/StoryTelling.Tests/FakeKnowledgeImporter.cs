using StoryTelling.Application.Knowledge;
using StoryTelling.Domain;

namespace StoryTelling.Tests;

internal sealed class FakeKnowledgeImporter : IKnowledgeImporter
{
    public IReadOnlyList<KnowledgeEntry> Entries { get; set; } = [];

    public KnowledgeImportRequest? LastRequest { get; private set; }

    public KnowledgeImportPlan Plan(string content) => new(0, KnowledgeImportRequest.DefaultMaxChunks);

    public Task<IReadOnlyList<KnowledgeEntry>> ExtractAsync(
        KnowledgeImportRequest request,
        IProgress<KnowledgeImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        progress?.Report(new KnowledgeImportProgress(1, 1));
        return Task.FromResult(Entries);
    }
}
