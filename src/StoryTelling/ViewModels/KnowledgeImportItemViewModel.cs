using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class KnowledgeImportItemViewModel : ObservableObject
{
    public KnowledgeImportItemViewModel(KnowledgeEntry entry) => Entry = new KnowledgeEntryEditorViewModel(entry);

    [ObservableProperty]
    private bool _isSelected = true;

    public KnowledgeEntryEditorViewModel Entry { get; }

    public string Title => Entry.Title;

    public string Kind => Entry.Kind.ToString();

    public string Tags => Entry.Tags;

    public string Content => Entry.Content;
}
