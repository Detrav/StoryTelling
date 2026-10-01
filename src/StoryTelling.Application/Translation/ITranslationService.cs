using StoryTelling.Application.Generation;

namespace StoryTelling.Application.Translation;

public interface ITranslationService
{
    Task<string> TranslateAsync(
        string text,
        string languageCode,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
