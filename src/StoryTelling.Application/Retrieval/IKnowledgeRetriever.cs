using StoryTelling.Domain;

namespace StoryTelling.Application.Retrieval;

public interface IKnowledgeRetriever
{
    IReadOnlyList<KnowledgeFragment> Search(string query, KnowledgeKind? kind, int topK);
}
