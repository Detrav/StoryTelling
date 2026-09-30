using CommunityToolkit.Mvvm.ComponentModel;
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
        Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
}
