using System.Text;
using StoryTelling.Domain;

namespace StoryTelling.Application.Knowledge;

public static class KnowledgeComposer
{
    public static List<KnowledgeEntry> Compose(Project project, int beforeChapterNumber)
    {
        var knowledge = project.Knowledge.Select(Clone).ToList();
        foreach (var chapter in project.Chapters.Where(chapter => chapter.Number < beforeChapterNumber).OrderBy(chapter => chapter.Number))
        {
            Apply(knowledge, chapter.KnowledgeChanges);
        }

        return knowledge;
    }

    public static void Apply(List<KnowledgeEntry> knowledge, IReadOnlyList<KnowledgeChange> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Operation)
            {
                case KnowledgeChangeOperation.Create:
                    ApplyCreate(knowledge, change);
                    break;
                case KnowledgeChangeOperation.Update:
                    ApplyUpdate(knowledge, change);
                    break;
                case KnowledgeChangeOperation.Delete:
                    knowledge.RemoveAll(entry => Matches(entry, change));
                    break;
            }
        }
    }

    private static void ApplyCreate(List<KnowledgeEntry> knowledge, KnowledgeChange change)
    {
        if (knowledge.FirstOrDefault(entry => Matches(entry, change)) is { } existing)
        {
            ApplyUpdate(knowledge, existing, change);
            return;
        }

        knowledge.Add(new KnowledgeEntry
        {
            Id = change.EntryId ?? Guid.NewGuid(),
            Kind = change.Kind,
            Title = change.Title.Trim(),
            Tags = [.. change.Tags],
            Content = change.Content,
            Status = change.Status,
        });
    }

    private static void ApplyUpdate(List<KnowledgeEntry> knowledge, KnowledgeChange change)
    {
        if (knowledge.FirstOrDefault(entry => Matches(entry, change)) is not { } entry)
        {
            ApplyCreate(knowledge, change);
            return;
        }

        ApplyUpdate(knowledge, entry, change);
    }

    private static void ApplyUpdate(List<KnowledgeEntry> knowledge, KnowledgeEntry entry, KnowledgeChange change)
    {
        entry.Kind = change.Kind;
        if (!string.IsNullOrWhiteSpace(change.Title))
        {
            entry.Title = change.Title.Trim();
        }

        entry.Tags = [.. change.Tags];
        entry.Content = change.Content;
        if (change.Status is { } status)
        {
            entry.Status = status;
        }
    }

    private static bool Matches(KnowledgeEntry entry, KnowledgeChange change) =>
        change.EntryId is { } id
            ? entry.Id == id
            : !string.IsNullOrWhiteSpace(change.Title)
              && string.Equals(Normalize(entry.Title), Normalize(change.Title), StringComparison.Ordinal);

    private static string Normalize(string title)
    {
        var trimmed = title.Trim();
        if (trimmed.StartsWith('[') && trimmed.IndexOf(']') is var close && close > 0 && close < trimmed.Length - 1)
        {
            trimmed = trimmed[(close + 1)..].Trim();
        }

        var builder = new StringBuilder(trimmed.Length);
        var pendingSpace = false;
        foreach (var ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch))
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(char.ToLowerInvariant(ch));
                pendingSpace = false;
            }
            else if (char.IsWhiteSpace(ch) || ch is '-' or '_')
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    private static KnowledgeEntry Clone(KnowledgeEntry entry) => new()
    {
        Id = entry.Id,
        Kind = entry.Kind,
        Title = entry.Title,
        Tags = [.. entry.Tags],
        Content = entry.Content,
        Status = entry.Status,
    };
}
