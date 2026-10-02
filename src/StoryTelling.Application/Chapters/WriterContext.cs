using StoryTelling.Domain;

namespace StoryTelling.Application.Chapters;

public sealed record WriterContext(
    Project Snapshot,
    Chapter Chapter,
    WorldState StateBefore,
    int TokenBudget = ChapterContextAssembler.DefaultTokenBudget,
    int RecentLoglineCount = 3,
    int RequiredSectionMaxChars = ChapterContextAssembler.DefaultRequiredSectionMaxChars);
