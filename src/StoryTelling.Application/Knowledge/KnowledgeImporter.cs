using System.Text.Json;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Retrieval;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;

namespace StoryTelling.Application.Knowledge;

public sealed class KnowledgeImporter : IKnowledgeImporter
{
    private const int _chunkChars = KnowledgeChunker.ImportMaxChars;

    private readonly ILlmClient _llmClient;
    private readonly ISettingsService _settingsService;

    public KnowledgeImporter(ILlmClient llmClient, ISettingsService settingsService)
    {
        _llmClient = llmClient;
        _settingsService = settingsService;
    }

    public KnowledgeImportPlan Plan(string content)
    {
        var chunks = KnowledgeChunker.Split(content, _chunkChars);
        return new KnowledgeImportPlan(chunks.Count, KnowledgeImportRequest.DefaultMaxChunks);
    }

    public async Task<IReadOnlyList<KnowledgeEntry>> ExtractAsync(
        KnowledgeImportRequest request,
        IProgress<KnowledgeImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return [];
        }

        IReadOnlyList<string> work = request.Mode == KnowledgeImportMode.Design
            ? [request.Content]
            : KnowledgeChunker.Split(request.Content, _chunkChars);
        if (work.Count == 0)
        {
            return [];
        }

        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        var connection = LlmConnection.From(settings.BaseUrl, settings.ApiKey, settings.TimeoutSeconds);

        var total = Math.Min(work.Count, Math.Max(1, request.MaxChunks));
        var entries = new List<KnowledgeEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < total; index++)
        {
            var llmRequest = new LlmRequest
            {
                Model = settings.Model,
                Messages = BuildMessages(request.Mode, work[index], request.Brief),
                Temperature = settings.TemperatureFor(LlmTask.Import),
                MaxTokens = settings.MaxTokens,
            };

            var content = await _llmClient
                .CompleteJsonAsync(connection, llmRequest, "KnowledgeEntries", KnowledgeImportSchema.Build(), cancellationToken)
                .ConfigureAwait(false);

            foreach (var entry in Parse(content))
            {
                if (seen.Add(entry.Title))
                {
                    entries.Add(entry);
                }
            }

            progress?.Report(new KnowledgeImportProgress(index + 1, total));
        }

        return entries;
    }

    private static IReadOnlyList<LlmMessage> BuildMessages(KnowledgeImportMode mode, string chunk, string brief) =>
        mode == KnowledgeImportMode.Design
            ? PromptTemplates.DesignKnowledge(chunk, brief)
            : PromptTemplates.ExtractKnowledge(chunk, brief);

    private static List<KnowledgeEntry> Parse(string content)
    {
        var result = new List<KnowledgeEntry>();

        try
        {
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var element in entries.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var title = GetString(element, "title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                result.Add(new KnowledgeEntry
                {
                    Kind = ParseKind(GetString(element, "kind")),
                    Title = title.Trim(),
                    Tags = GetTags(element),
                    Content = GetString(element, "content"),
                });
            }
        }
        catch (JsonException)
        {
        }

        return result;
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static List<string> GetTags(JsonElement element)
    {
        var tags = new List<string>();
        if (element.TryGetProperty("tags", out var value) && value.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in value.EnumerateArray())
            {
                if (tag.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(tag.GetString()))
                {
                    tags.Add(tag.GetString()!.Trim());
                }
            }
        }

        return tags;
    }

    private static KnowledgeKind ParseKind(string value) =>
        Enum.TryParse<KnowledgeKind>(value, ignoreCase: true, out var kind) ? kind : KnowledgeKind.Note;
}
