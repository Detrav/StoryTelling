namespace StoryTelling.Application.Chapters;

public interface IContextAssembler
{
    ChapterContext AssembleWriter(WriterContext context);
}
