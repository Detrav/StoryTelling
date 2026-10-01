using StoryTelling.Application.Retrieval;
using StoryTelling.Domain;

namespace StoryTelling.Application.Story;

public sealed class StoryQuery
{
    private readonly Project _project;
    private readonly IKnowledgeRetriever _retriever;

    public StoryQuery(Project project)
    {
        _project = project;
        _retriever = new Bm25KnowledgeRetriever(project.Knowledge);
    }

    public StoryOverview Story()
    {
        var world = _project.World;
        return new StoryOverview(
            _project.Name,
            world.Genre,
            world.Tone,
            world.Style,
            world.PointOfView,
            world.Tense,
            world.Rating,
            world.Title,
            world.Body);
    }

    public IReadOnlyList<KnowledgeSummary> Characters() =>
        ListEntries(KnowledgeKind.Character);

    public KnowledgeEntry? Character(string name) =>
        _project.Knowledge.FirstOrDefault(entry =>
            entry.Kind == KnowledgeKind.Character
            && string.Equals(entry.Title, name?.Trim(), StringComparison.OrdinalIgnoreCase));

    public WorldState InitialWorldState() => _project.InitialWorldState;

    public IReadOnlyList<ChapterLogline> RecentLoglines(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        return _project.Chapters
            .Where(chapter => !string.IsNullOrWhiteSpace(chapter.Logline))
            .OrderBy(chapter => chapter.Number)
            .TakeLast(count)
            .Select(chapter => new ChapterLogline(chapter.Number, chapter.Title, chapter.Logline))
            .ToList();
    }

    public IReadOnlyList<KnowledgeSummary> ListEntries(KnowledgeKind? kind) =>
        _project.Knowledge
            .Where(entry => kind is null || entry.Kind == kind)
            .Select(entry => new KnowledgeSummary(entry.Id, entry.Kind, entry.Title, [.. entry.Tags]))
            .ToList();

    public KnowledgeEntry? GetEntry(string idOrTitle)
    {
        if (Guid.TryParse(idOrTitle, out var id))
        {
            var byId = _project.Knowledge.FirstOrDefault(entry => entry.Id == id);
            if (byId is not null)
            {
                return byId;
            }
        }

        return _project.Knowledge.FirstOrDefault(entry => string.Equals(entry.Title, idOrTitle?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<KnowledgeFragment> SearchKnowledge(string query, KnowledgeKind? kind, int topK) =>
        _retriever.Search(query, kind, topK);
}
