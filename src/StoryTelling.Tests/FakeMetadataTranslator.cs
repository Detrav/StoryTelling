using StoryTelling.Application.Generation;
using StoryTelling.Application.Translation;

namespace StoryTelling.Tests;

internal sealed class FakeMetadataTranslator : IMetadataTranslator
{
    private readonly List<string> _calls = [];

    public MetadataTranslationResult Result { get; set; } =
        new("Translated name", "Translated annotation", new Dictionary<int, string>());

    public Func<MetadataTranslationRequest, MetadataTranslationResult>? ResultFactory { get; set; }

    public Exception? Failure { get; set; }

    public IReadOnlyList<string> Calls => _calls;

    public MetadataTranslationRequest? LastRequest { get; private set; }

    public Task<MetadataTranslationResult> TranslateAsync(
        MetadataTranslationRequest request,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _calls.Add(request.LanguageCode);
        LastRequest = request;
        progress?.Report(new GenerationProgress("Translating metadata", 0));

        if (Failure is not null)
        {
            return Task.FromException<MetadataTranslationResult>(Failure);
        }

        return Task.FromResult(ResultFactory?.Invoke(request) ?? Result);
    }
}
