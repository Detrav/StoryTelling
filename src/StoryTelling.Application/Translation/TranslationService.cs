using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Settings;

namespace StoryTelling.Application.Translation;

public sealed class TranslationService : ITranslationService
{
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
            Temperature = settings.TemperatureFor(LlmTask.Translation),
            ReasoningEffort = settings.ReasoningEffortFor(LlmTask.Translation),
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

        return await RepairAsync(connection, settings, text, translated, languageCode, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> RepairAsync(
        LlmConnection connection,
        AppSettings settings,
        string source,
        string translated,
        string languageCode,
        IProgress<GenerationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var expected = LanguageScripts.For(languageCode);
        if (expected == CharScript.None)
        {
            return translated;
        }

        var sourceParagraphs = TranslationQuality.SplitParagraphs(source);
        var targetParagraphs = TranslationQuality.SplitParagraphs(translated);
        if (sourceParagraphs.Count != targetParagraphs.Count || sourceParagraphs.Count == 0)
        {
            return translated;
        }

        var suspects = TranslationQuality.SuspectParagraphs(targetParagraphs, expected);
        if (suspects.Count == 0)
        {
            return translated;
        }

        progress?.Report(new GenerationProgress($"Fixing {suspects.Count} paragraph(s)", 0));

        var repaired = false;
        foreach (var index in suspects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var paragraph = await TranslateOneAsync(connection, settings, sourceParagraphs[index], languageCode, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(paragraph))
            {
                targetParagraphs[index] = paragraph.Trim();
                repaired = true;
            }
        }

        return repaired ? string.Join("\n\n", targetParagraphs) : translated;
    }

    private async Task<string> TranslateOneAsync(
        LlmConnection connection,
        AppSettings settings,
        string paragraph,
        string languageCode,
        CancellationToken cancellationToken)
    {
        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = PromptTemplates.BuildTranslation(paragraph, languageCode),
            Temperature = settings.TemperatureFor(LlmTask.Translation),
            ReasoningEffort = settings.ReasoningEffortFor(LlmTask.Translation),
            MaxTokens = settings.MaxTokens,
        };

        var completion = await _llmClient.CompleteAsync(connection, request, cancellationToken).ConfigureAwait(false);
        return GeneratedText.StripCodeFence(completion.Content.Trim());
    }
}
