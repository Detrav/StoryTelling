using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class KnowledgeChangeEditorViewModel : ObservableObject
{
    private static readonly IReadOnlyList<KnowledgeChangeOperation> _operations = Enum.GetValues<KnowledgeChangeOperation>();

    private static readonly IReadOnlyList<KnowledgeKind> _kinds = Enum.GetValues<KnowledgeKind>();

    private static readonly IBrush _createBrush = new SolidColorBrush(Color.Parse("#DCF5DC"));

    private static readonly IBrush _updateBrush = new SolidColorBrush(Color.Parse("#FBF3C0"));

    private static readonly IBrush _deleteBrush = new SolidColorBrush(Color.Parse("#F8D7DA"));

    public KnowledgeChangeEditorViewModel()
    {
    }

    public KnowledgeChangeEditorViewModel(KnowledgeChange change)
    {
        _operation = change.Operation;
        _kind = change.Kind;
        _title = change.Title;
        _tags = string.Join(", ", change.Tags);
        _content = change.Content;
        _reason = change.Reason;
    }

    public IReadOnlyList<KnowledgeChangeOperation> Operations => _operations;

    public IReadOnlyList<KnowledgeKind> Kinds => _kinds;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Background))]
    private KnowledgeChangeOperation _operation = KnowledgeChangeOperation.Update;

    [ObservableProperty]
    private KnowledgeKind _kind = KnowledgeKind.Note;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTitle))]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _tags = string.Empty;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private string _reason = string.Empty;

    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);

    public IReadOnlyList<string> TagList =>
        Tags.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IBrush Background => Operation switch
    {
        KnowledgeChangeOperation.Create => _createBrush,
        KnowledgeChangeOperation.Delete => _deleteBrush,
        _ => _updateBrush,
    };

    public KnowledgeChange ToChange() => new()
    {
        Operation = Operation,
        Kind = Kind,
        Title = Title.Trim(),
        Tags = [.. TagList],
        Content = Content,
        Reason = Reason,
    };

    public KnowledgeChangeEditorViewModel Clone() => new(ToChange());

    public void CopyFrom(KnowledgeChangeEditorViewModel other)
    {
        Operation = other.Operation;
        Kind = other.Kind;
        Title = other.Title;
        Tags = other.Tags;
        Content = other.Content;
        Reason = other.Reason;
    }
}
