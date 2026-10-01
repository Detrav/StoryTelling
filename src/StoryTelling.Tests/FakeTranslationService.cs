using StoryTelling.Application.Generation;
using StoryTelling.Application.Translation;

namespace StoryTelling.Tests;

internal sealed class FakeTranslationService : ITranslationService
{
    public string Result { get; set; } = "Translated.";

    public string? LastLanguageCode { get; private set; }

    public Task<string> TranslateAsync(
        string text,
        string languageCode,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        LastLanguageCode = languageCode;
        return Task.FromResult(Result);
    }
}
