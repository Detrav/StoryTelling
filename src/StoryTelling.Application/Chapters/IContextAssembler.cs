namespace StoryTelling.Application.Chapters;

public interface IContextAssembler
{
    ChapterContext AssembleWriter(WriterContext context);

    string BuildStoryContext(WriterContext context);
}
