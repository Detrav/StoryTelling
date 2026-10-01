using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Settings;

namespace StoryTelling.Application.Translation;

public sealed class TranslationService : ITranslationService
{
    private const double DeterministicTemperature = 0.2;

    private readonly ILlmClient _llmClient;
    private readonly ISettingsService _settingsService;

    public TranslationService(ILlmClient llmClient, ISettingsService settingsService)
    {
        _llmClient = llmClient;
        _settingsService = settingsService;
    }

    public async Task<string> TranslateAsync(
        string text,
        string languageCode,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(languageCode))
        {
            return text;
        }

        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);

        progress?.Report(new GenerationProgress("Translating", 0));

        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = PromptTemplates.BuildTranslation(text, languageCode),
            Temperature = Math.Min(settings.Temperature, DeterministicTemperature),
            MaxTokens = settings.MaxTokens,
        };

        var completion = await _llmClient.CompleteAsync(connection, request, cancellationToken).ConfigureAwait(false);
        var translated = GeneratedText.StripCodeFence(completion.Content.Trim());

        if (GeneratedText.LooksTruncated(translated))
        {
            var retry = request with
            {
                Messages =
                [
                    .. request.Messages,
                    LlmMessage.Assistant(completion.Content),
                    LlmMessage.User("Your previous reply was cut off. Return the complete translation and end with a full sentence."),
                ],
            };

            var completed = await _llmClient.CompleteAsync(connection, retry, cancellationToken).ConfigureAwait(false);
            var completedText = GeneratedText.StripCodeFence(completed.Content.Trim());
            if (completedText.Length >= translated.Length)
            {
                translated = completedText;
            }
        }

        if (string.IsNullOrWhiteSpace(translated))
        {
            throw new LlmException(LlmErrorKind.InvalidResponse, "The model returned an empty translation.");
        }

        return translated;
    }
}
