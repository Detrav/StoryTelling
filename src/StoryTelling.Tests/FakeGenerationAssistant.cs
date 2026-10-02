using StoryTelling.Application.Generation;

namespace StoryTelling.Tests;

internal sealed class FakeGenerationAssistant : IGenerationAssistant
{
    public IReadOnlyList<GenerationOption> Options { get; set; } =
    [
        new GenerationOption(new Dictionary<string, string>
        {
            ["WorldTitle"] = "Title A",
            ["WorldBody"] = "Body A",
        }),
    ];

    public GenerationRequest? LastRequest { get; private set; }

    public Exception? Throws { get; set; }

    public Task<IReadOnlyList<GenerationOption>> GenerateAsync(
        GenerationRequest request,
        GenerationSession? session = null,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Throws is null
            ? Task.FromResult(Options)
            : Task.FromException<IReadOnlyList<GenerationOption>>(Throws);
    }
}
