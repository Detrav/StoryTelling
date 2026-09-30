namespace StoryTelling.Application.Generation;

public interface IGenerationAssistant
{
    Task<IReadOnlyList<GenerationOption>> GenerateAsync(
        GenerationRequest request,
        GenerationSession? session = null,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
