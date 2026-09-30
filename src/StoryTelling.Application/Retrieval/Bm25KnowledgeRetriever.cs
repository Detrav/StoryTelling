using System.Text.RegularExpressions;
using StoryTelling.Domain;

namespace StoryTelling.Application.Retrieval;

public sealed class Bm25KnowledgeRetriever : IKnowledgeRetriever
{
    private const double _k1 = 1.5;
    private const double _b = 0.75;

    private static readonly Regex _tokenizer = new("[^\\p{L}\\p{N}]+", RegexOptions.Compiled);

    private readonly IReadOnlyList<(KnowledgeFragment Fragment, string[] Tokens)> _documents;
    private readonly Dictionary<string, int> _documentFrequency;
    private readonly double _averageLength;

    public Bm25KnowledgeRetriever(IEnumerable<KnowledgeEntry> entries, int maxChars = KnowledgeChunker.DefaultMaxChars)
    {
        var documents = new List<(KnowledgeFragment, string[])>();
        var documentFrequency = new Dictionary<string, int>();

        foreach (var entry in entries)
        {
            var chunks = KnowledgeChunker.Split(entry.Content, maxChars);
            for (var index = 0; index < chunks.Count; index++)
            {
                var tokens = Tokenize($"{entry.Title} {chunks[index]} {string.Join(' ', entry.Tags)}");
                documents.Add((new KnowledgeFragment(entry.Id, entry.Title, entry.Kind, index, chunks[index]), tokens));

                foreach (var token in tokens.Distinct())
                {
                    documentFrequency[token] = documentFrequency.GetValueOrDefault(token) + 1;
                }
            }
        }

        _documents = documents;
        _documentFrequency = documentFrequency;
        _averageLength = documents.Count == 0 ? 0 : documents.Average(document => document.Item2.Length);
    }

    public IReadOnlyList<KnowledgeFragment> Search(string query, KnowledgeKind? kind, int topK)
    {
        if (_documents.Count == 0 || topK <= 0 || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var queryTerms = Tokenize(query).Distinct().ToList();
        var scored = new List<(KnowledgeFragment Fragment, double Score)>();

        foreach (var (fragment, tokens) in _documents)
        {
            if (kind is not null && fragment.Kind != kind)
            {
                continue;
            }

            var length = tokens.Length == 0 ? 1 : tokens.Length;
            var score = 0.0;

            foreach (var term in queryTerms)
            {
                if (!_documentFrequency.TryGetValue(term, out var frequency))
                {
                    continue;
                }

                var termFrequency = tokens.Count(token => token == term);
                if (termFrequency == 0)
                {
                    continue;
                }

                var idf = Math.Log(1 + (_documents.Count - frequency + 0.5) / (frequency + 0.5));
                score += idf * termFrequency * (_k1 + 1) / (termFrequency + _k1 * (1 - _b + _b * length / _averageLength));
            }

            if (score > 0)
            {
                scored.Add((fragment, score));
            }
        }

        return scored
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Fragment.Title, StringComparer.Ordinal)
            .ThenBy(item => item.Fragment.Index)
            .Take(topK)
            .Select(item => item.Fragment)
            .ToList();
    }

    private static string[] Tokenize(string text) =>
        _tokenizer.Split(text.ToLowerInvariant()).Where(token => token.Length > 0).ToArray();
}
