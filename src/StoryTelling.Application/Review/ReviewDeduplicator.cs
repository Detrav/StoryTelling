using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.Application.Review;

public static class ReviewDeduplicator
{
    private const double StrongSimilarity = 0.72;

    private const double ReferenceSimilarity = 0.5;

    private static readonly HashSet<string> _stopWords =
    [
        "a", "an", "the", "and", "or", "for", "of", "to", "in", "on", "at", "by", "with",
        "across", "between", "entry", "entries", "issue", "problem", "inconsistency",
        "inconsistencies", "conflict", "contradiction", "required", "needed", "missing",
    ];

    public static IReadOnlyList<ReviewFinding> Deduplicate(IReadOnlyList<ReviewFinding> findings, Project snapshot)
    {
        var clusters = new List<List<FindingView>>();

        for (var index = 0; index < findings.Count; index++)
        {
            var view = Build(findings[index], snapshot, index);
            var target = clusters.FirstOrDefault(cluster => Matches(cluster[0], view));
            if (target is null)
            {
                clusters.Add([view]);
            }
            else
            {
                target.Add(view);
            }
        }

        return [.. clusters.Select(Merge)];
    }

    private static bool Matches(FindingView left, FindingView right)
    {
        if (!string.Equals(left.Finding.Area, right.Finding.Area))
        {
            return false;
        }

        if (left.TitleNorm.Length > 0 && left.TitleNorm == right.TitleNorm)
        {
            return true;
        }

        var similarity = Similarity(left, right);
        if (similarity >= StrongSimilarity)
        {
            return true;
        }

        return left.Reference is not null
            && left.Reference == right.Reference
            && similarity >= ReferenceSimilarity;
    }

    private static double Similarity(FindingView left, FindingView right) =>
        (0.50 * Jaccard(left.TitleTokens, right.TitleTokens))
        + (0.20 * Jaccard(left.AllTokens, right.AllTokens))
        + (0.30 * TrigramDice(left.TitleNorm, right.TitleNorm));

    private static ReviewFinding Merge(List<FindingView> cluster)
    {
        var representative = cluster
            .OrderByDescending(view => view.Finding.Severity)
            .ThenByDescending(view => view.Finding.Fix is { IsEmpty: false })
            .ThenByDescending(view => !string.IsNullOrWhiteSpace(view.Finding.Suggestion))
            .ThenByDescending(view => view.Finding.Detail.Length)
            .ThenBy(view => view.Index)
            .First();

        var detail = cluster.OrderByDescending(view => view.Finding.Detail.Length).First().Finding.Detail;
        var suggestion = cluster.Select(view => view.Finding.Suggestion).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var reference = cluster.Select(view => view.Reference).FirstOrDefault(value => value is not null);
        var edits = cluster
            .SelectMany(view => view.Finding.Fix?.Edits ?? [])
            .ToList();

        return representative.Finding with
        {
            Detail = detail,
            Suggestion = suggestion,
            Reference = reference,
            Fix = BuildFix(edits, representative.Snapshot),
        };
    }

    private static ReviewFix? BuildFix(IReadOnlyList<ReviewEdit> edits, Project snapshot)
    {
        var kept = new List<ReviewEdit>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var created = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var edit in edits)
        {
            var key = edit.Operation == ReviewEditOperation.Set
                ? $"Set|{edit.Target}|{edit.Reference?.Trim()}|{edit.Field}"
                : $"{edit.Operation}|{edit.Target}|{edit.Reference?.Trim()}|{edit.Value}";
            if (!seen.Add(key) || IsNoOp(edit, snapshot, created))
            {
                continue;
            }

            if (edit.Operation == ReviewEditOperation.Create && edit.Reference?.Trim() is { Length: > 0 } title)
            {
                created.Add(title);
            }

            kept.Add(edit);
        }

        return kept.Count == 0 ? null : new ReviewFix(kept);
    }

    private static bool IsNoOp(ReviewEdit edit, Project snapshot, IReadOnlySet<string> created)
    {
        if (edit.Target != GenerationTarget.Knowledge)
        {
            return false;
        }

        var reference = edit.Reference?.Trim() ?? string.Empty;
        var inFix = created.Contains(reference);
        var entry = snapshot.Knowledge.FirstOrDefault(candidate =>
            string.Equals(candidate.Title, reference, StringComparison.OrdinalIgnoreCase));

        switch (edit.Operation)
        {
            case ReviewEditOperation.Create:
                return entry is not null || inFix;
            case ReviewEditOperation.Delete:
                return entry is null && !inFix;
            case ReviewEditOperation.AddTag:
                return entry is null
                    ? !inFix
                    : entry.Tags.Any(tag => string.Equals(tag, edit.Value.Trim(), StringComparison.OrdinalIgnoreCase));
            case ReviewEditOperation.RemoveTag:
                return entry is null
                    ? !inFix
                    : !entry.Tags.Any(tag => string.Equals(tag, edit.Value.Trim(), StringComparison.OrdinalIgnoreCase));
            default:
                if (entry is null)
                {
                    return false;
                }

                var current = CurrentValue(entry, edit.Field);
                return current is not null && string.Equals(Collapse(current), Collapse(edit.Value), StringComparison.Ordinal);
        }
    }

    private static string? CurrentValue(KnowledgeEntry entry, string field) => field.Trim().ToLowerInvariant() switch
    {
        "title" => entry.Title,
        "kind" => entry.Kind.ToString(),
        "content" => entry.Content,
        "tags" => string.Join(", ", entry.Tags),
        _ => null,
    };

    private static FindingView Build(ReviewFinding finding, Project snapshot, int index) => new(
        finding,
        NormalizeTitle(finding.Title),
        Tokens(finding.Title, includeStopWords: false),
        Tokens($"{finding.Title} {finding.Detail}", includeStopWords: false),
        CanonicalReference(finding.Reference, snapshot),
        snapshot,
        index);

    private static string NormalizeTitle(string title) =>
        string.Join(' ', new string(title.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : ' ').ToArray())
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? CanonicalReference(string? reference, Project snapshot)
    {
        var collapsed = Collapse(reference).ToLowerInvariant();
        if (collapsed.Length == 0)
        {
            return null;
        }

        var exact = snapshot.Knowledge.FirstOrDefault(entry =>
            string.Equals(Collapse(entry.Title).ToLowerInvariant(), collapsed, StringComparison.Ordinal));
        if (exact is not null)
        {
            return exact.Title;
        }

        var lastWord = collapsed.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        var surnameMatches = lastWord is null
            ? []
            : snapshot.Knowledge
                .Where(entry => entry.Title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Any(word => string.Equals(word, lastWord, StringComparison.OrdinalIgnoreCase)))
                .ToList();

        return surnameMatches.Count == 1 ? surnameMatches[0].Title : Collapse(reference);
    }

    private static IReadOnlySet<string> Tokens(string text, bool includeStopWords)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = new string(raw.Where(char.IsLetterOrDigit).ToArray());
            if (token.Length <= 1 || (!includeStopWords && _stopWords.Contains(token)))
            {
                continue;
            }

            tokens.Add(token.Length > 3 && token.EndsWith('s') ? token[..^1] : token);
        }

        return tokens;
    }

    private static double Jaccard(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        if (left.Count == 0 && right.Count == 0)
        {
            return 0;
        }

        var intersection = left.Count(right.Contains);
        var union = left.Count + right.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static double TrigramDice(string left, string right)
    {
        var leftTrigrams = Trigrams(left);
        var rightTrigrams = Trigrams(right);
        if (leftTrigrams.Count == 0 || rightTrigrams.Count == 0)
        {
            return 0;
        }

        var intersection = leftTrigrams.Count(rightTrigrams.Contains);
        return 2.0 * intersection / (leftTrigrams.Count + rightTrigrams.Count);
    }

    private static IReadOnlySet<string> Trigrams(string value)
    {
        var text = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var trigrams = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index + 3 <= text.Length; index++)
        {
            trigrams.Add(text.Substring(index, 3));
        }

        return trigrams;
    }

    private static string Collapse(string? value) =>
        value is null ? string.Empty : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private sealed record FindingView(
        ReviewFinding Finding,
        string TitleNorm,
        IReadOnlySet<string> TitleTokens,
        IReadOnlySet<string> AllTokens,
        string? Reference,
        Project Snapshot,
        int Index);
}
