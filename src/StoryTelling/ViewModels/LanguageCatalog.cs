using System.Collections.ObjectModel;

namespace StoryTelling.ViewModels;

public sealed class LanguageCatalog
{
    public ObservableCollection<LanguageOption> Items { get; } =
    [
        new("ru", "Russian"),
        new("de", "German"),
        new("es", "Spanish"),
        new("fr", "French"),
    ];

    public string DefaultCode { get; set; } = "ru";
}
