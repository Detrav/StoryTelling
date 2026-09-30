namespace StoryTelling.Application.Generation;

public interface IGenerationAssistant
{
    Task<IReadOnlyList<GenerationOption>> GenerateAsync(
        GenerationRequest request,
        CancellationToken cancellationToken = default);
}
