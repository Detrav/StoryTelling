using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Application.Generation;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class KnowledgeEntryEditorViewModel : ObservableObject
{
    private static readonly IReadOnlyList<KnowledgeKind> _allKinds = Enum.GetValues<KnowledgeKind>();

    public KnowledgeEntryEditorViewModel()
    {
    }

    public KnowledgeEntryEditorViewModel(KnowledgeEntry entry)
    {
        Id = entry.Id;
        _kind = entry.Kind;
        _title = entry.Title;
        _tags = string.Join(", ", entry.Tags);
        _content = entry.Content;
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Func<string, int, GenerationSession, IProgress<GenerationProgress>?, CancellationToken, Task<IReadOnlyList<GenerationOption>>>? GenerateOptions { get; set; }

    public IReadOnlyList<KnowledgeKind> Kinds => _allKinds;

    [ObservableProperty]
    private KnowledgeKind _kind = KnowledgeKind.Note;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTitle))]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _tags = string.Empty;

    [ObservableProperty]
    private string _content = string.Empty;

    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);

    public IReadOnlyList<string> TagList =>
        Tags.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public KnowledgeEntry ToEntry() => new()
    {
        Id = Id,
        Kind = Kind,
        Title = Title.Trim(),
        Tags = [.. TagList],
        Content = Content,
    };

    public KnowledgeEntryEditorViewModel Clone() => new(ToEntry());

    public void CopyFrom(KnowledgeEntryEditorViewModel other)
    {
        Kind = other.Kind;
        Title = other.Title;
        Tags = other.Tags;
        Content = other.Content;
    }

    public IReadOnlyDictionary<string, string> ToFields() => new Dictionary<string, string>
    {
        ["Kind"] = Kind.ToString(),
        ["Title"] = Title,
        ["Tags"] = Tags,
        ["Content"] = Content,
    };

    public void ApplyFields(IReadOnlyDictionary<string, string> fields)
    {
        Kind = GetKind(fields, "Kind", Kind);
        Title = Get(fields, "Title", Title);
        Tags = Get(fields, "Tags", Tags);
        Content = Get(fields, "Content", Content);
    }

    private static string Get(IReadOnlyDictionary<string, string> fields, string key, string fallback) =>
        fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;

    private static KnowledgeKind GetKind(IReadOnlyDictionary<string, string> fields, string key, KnowledgeKind fallback) =>
        fields.TryGetValue(key, out var value) && Enum.TryParse<KnowledgeKind>(value, ignoreCase: true, out var kind) ? kind : fallback;
}
