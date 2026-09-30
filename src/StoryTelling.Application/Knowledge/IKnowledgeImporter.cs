using StoryTelling.Domain;

namespace StoryTelling.Application.Knowledge;

public interface IKnowledgeImporter
{
    Task<IReadOnlyList<KnowledgeEntry>> ExtractAsync(
        KnowledgeImportRequest request,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
