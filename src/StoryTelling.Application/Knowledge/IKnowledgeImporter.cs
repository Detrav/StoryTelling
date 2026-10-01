using StoryTelling.Domain;

namespace StoryTelling.Application.Knowledge;

public interface IKnowledgeImporter
{
    KnowledgeImportPlan Plan(string content);

    Task<IReadOnlyList<KnowledgeEntry>> ExtractAsync(
        KnowledgeImportRequest request,
        IProgress<KnowledgeImportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
