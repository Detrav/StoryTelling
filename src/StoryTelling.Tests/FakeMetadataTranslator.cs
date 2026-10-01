using StoryTelling.Application.Generation;
using StoryTelling.Application.Translation;

namespace StoryTelling.Tests;

internal sealed class FakeMetadataTranslator : IMetadataTranslator
{
    public MetadataTranslationResult Result { get; set; } =
        new("Translated name", "Translated annotation", new Dictionary<int, string>());

    public MetadataTranslationRequest? LastRequest { get; private set; }

    public Task<MetadataTranslationResult> TranslateAsync(
        MetadataTranslationRequest request,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(Result);
    }
}
