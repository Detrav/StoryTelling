using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StoryTelling.Application.Settings;
using StoryTelling.Application.Translation;
using StoryTelling.Domain;

namespace StoryTelling.ViewModels;

public partial class ExportViewModel : ViewModelBase
{
    public ExportViewModel(Project project, IReadOnlyList<LanguageData> catalog)
    {
        Project = project;

        var chapters = project.Chapters.Where(chapter => !string.IsNullOrWhiteSpace(chapter.ContentOriginal)).ToList();
        var total = chapters.Count;

        Languages.Add(new ExportLanguage("en", "English (original)", total, total, MetadataTranslationCoverage.Evaluate(project, "en")));

        foreach (var code in project.Settings.TargetLanguages)
        {
            if (string.Equals(code, "en", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = catalog
                .FirstOrDefault(language => string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase))?.DisplayName
                ?? code.ToUpperInvariant();
            var translated = chapters.Count(chapter =>
                chapter.Translations.TryGetValue(code, out var text) && !string.IsNullOrWhiteSpace(text));
            Languages.Add(new ExportLanguage(code, name, translated, total, MetadataTranslationCoverage.Evaluate(project, code)));
        }

        Selected = Languages[0];
    }

    public Project Project { get; }

    public ObservableCollection<ExportLanguage> Languages { get; } = [];

    [ObservableProperty]
    private ExportLanguage? _selected;
}
