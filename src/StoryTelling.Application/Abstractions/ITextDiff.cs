using StoryTelling.Application.Undo;

namespace StoryTelling.Application.Abstractions;

public interface ITextDiff
{
    TextPatch CreatePatch(string oldText, string newText);
}
