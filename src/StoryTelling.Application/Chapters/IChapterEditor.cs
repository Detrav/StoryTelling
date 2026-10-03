using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public interface IChapterEditor
{
    Task<EditorChecklistVerdict> CheckAsync(
        Project project,
        Chapter chapter,
        string text,
        WorldState stateBefore,
        IReadOnlyList<EditorCheck> checks,
        CancellationToken cancellationToken = default);

    Task<string> FixAsync(
        Project project,
        Chapter chapter,
        string text,
        WorldState stateBefore,
        EditorCheck check,
        string reason,
        CancellationToken cancellationToken = default);

    Task<ChapterEdit> CosmeticAsync(
        Project project,
        Chapter chapter,
        string text,
        WorldState stateBefore,
        CancellationToken cancellationToken = default);
}
