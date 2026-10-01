using StoryTelling.Application.Generation;

namespace StoryTelling.Application.Translation;

public interface IMetadataTranslator
{
    Task<MetadataTranslationResult> TranslateAsync(
        MetadataTranslationRequest request,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}