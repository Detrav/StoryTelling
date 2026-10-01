using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Knowledge;
using StoryTelling.Application.Review;
using StoryTelling.Application.Translation;
using StoryTelling.Infrastructure;
using StoryTelling.Infrastructure.Diff;
using StoryTelling.Infrastructure.Llm;
using StoryTelling.Infrastructure.Logging;
using StoryTelling.ViewModels;

namespace StoryTelling;

internal static class AppServices
{
    public static ServiceProvider Provider { get; private set; } = null!;

    public static void Initialize()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new FileLoggerProvider(AppPaths.CreateLogFilePath()));
        });

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IGuidGenerator, GuidGenerator>();
        services.AddSingleton<IProjectRepository, JsonProjectRepository>();
        services.AddSingleton<ISettingsService>(new JsonSettingsService(AppPaths.SettingsFile));
        services.AddSingleton<ITextDiff, DiffPlexTextDiff>();
        services.AddSingleton(new HttpClient { Timeout = Timeout.InfiniteTimeSpan });
        services.AddSingleton<ILlmClient, OpenAiCompatibleLlmClient>();
        services.AddSingleton<IGenerationAssistant, GenerationAssistant>();
        services.AddSingleton<IKnowledgeImporter, KnowledgeImporter>();
        services.AddSingleton<IProjectReviewAssistant, ProjectReviewAssistant>();
        services.AddSingleton<IContextAssembler, ChapterContextAssembler>();
        services.AddSingleton<IChapterAgent, ChapterAgent>();
        services.AddSingleton<IChapterEditor, ChapterEditor>();
        services.AddSingleton<IChapterSummarizer, ChapterSummarizer>();
        services.AddSingleton<IChapterWorkflow, ChapterWorkflow>();
        services.AddSingleton<IChapterRunner, ChapterRunner>();
        services.AddSingleton<ITranslationService, TranslationService>();
        services.AddSingleton<MainWindowViewModel>();

        Provider = services.BuildServiceProvider();
    }
}
