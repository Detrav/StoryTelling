using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StoryTelling.Application.Abstractions;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Export;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Knowledge;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Review;
using StoryTelling.Application.Settings;
using StoryTelling.Application.Translation;
using StoryTelling.Domain;
using StoryTelling.Infrastructure;
using StoryTelling.Infrastructure.Diff;
using StoryTelling.Infrastructure.Llm;

namespace StoryTelling.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var command = args.Length == 0 ? "help" : args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToArray();

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        var token = cancellation.Token;

        try
        {
            return command switch
            {
                "ping" => await PingAsync(rest, token),
                "gen" => await GenAsync(rest, token),
                "setup" => await SetupAsync(rest, token),
                "write" => await WriteAsync(rest, token),
                "summarize" => await SummarizeAsync(rest, token),
                "translate" => await TranslateAsync(rest, token),
                "design" => await DesignAsync(rest, token),
                "export" => await ExportAsync(rest, token),
                "recompute" => await RecomputeAsync(rest, token),
                "context" => await ContextAsync(rest, token),
                "regenerate" => await RegenerateAsync(rest, token),
                "judge" => await JudgeAsync(rest, token),
                "compare" => await CompareAsync(rest, token),
                "experiment" => await ExperimentAsync(rest, token),
                "continuity" => await ContinuityAsync(rest, token),
                "review" => await ReviewAsync(rest, token),
                "create" => await CreateAsync(rest, token),
                _ => Help(),
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 130;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }
    }

    private static async Task<CliContext> BuildAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var settings = await LoadSettingsAsync(args, cancellationToken);
        var httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var llmClient = new OpenAiCompatibleLlmClient(httpClient);
        return new CliContext(settings, httpClient, llmClient, new CliSettingsService(settings));
    }

    private static async Task<AppSettings> LoadSettingsAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        AppSettings settings;
        try
        {
            settings = await new JsonSettingsService(AppPaths.SettingsFile).LoadAsync(cancellationToken);
        }
        catch
        {
            settings = AppSettings.CreateDefault();
        }

        settings.BaseUrl = ArgReader.String(args, "--base-url", settings.BaseUrl);
        settings.Model = ArgReader.String(args, "--model", settings.Model);
        settings.ApiKey = ArgReader.String(args, "--api-key", settings.ApiKey);
        settings.MaxTokens = ArgReader.Int(args, "--max-tokens", settings.MaxTokens);
        settings.MaxToolCalls = ArgReader.Int(args, "--max-tool-calls", settings.MaxToolCalls);
        settings.ContextTokenBudget = ArgReader.Int(args, "--context-token-budget", settings.ContextTokenBudget);
        settings.RecentLoglineCount = ArgReader.Int(args, "--recent-loglines", settings.RecentLoglineCount);
        settings.ContextRequiredSectionMaxChars = ArgReader.Int(args, "--required-cap", settings.ContextRequiredSectionMaxChars);
        settings.ToolResultMaxChars = ArgReader.Int(args, "--tool-result-max-chars", settings.ToolResultMaxChars);
        settings.Temperature = ArgReader.Double(args, "--temperature", settings.Temperature);
        settings.TimeoutSeconds = ArgReader.Int(args, "--timeout", settings.TimeoutSeconds);
        return settings;
    }

    private static async Task<int> PingAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        using var context = await BuildAsync(args, cancellationToken);
        Console.WriteLine($"provider: {context.Settings.BaseUrl}");
        Console.WriteLine($"model:    {context.Settings.Model}");

        var support = await context.LlmClient.CheckStructuredOutputAsync(context.Connection, context.Settings.Model, cancellationToken);
        Console.WriteLine($"structured output: {(support.Supported ? "supported" : "not supported")}{Detail(support.Detail)}");

        var completion = await context.LlmClient.CompleteAsync(context.Connection, new LlmRequest
        {
            Model = context.Settings.Model,
            Messages = [LlmMessage.User("Reply with the single word: pong.")],
            Temperature = 0,
            MaxTokens = 512,
        }, cancellationToken);

        Console.WriteLine($"completion: {completion.Content.Trim()}");
        return 0;
    }

    private static async Task<int> GenAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var targetName = ArgReader.Value(args, "--target");
        var file = ArgReader.Value(args, "--file");
        if (targetName is null || !Enum.TryParse<GenerationTarget>(targetName, ignoreCase: true, out var target) || file is null)
        {
            Console.Error.WriteLine("usage: storydev gen --target <Target> --file <path> [--variants N] [--brief ...] [--out <path>] [--replace]");
            return 2;
        }

        var variants = ArgReader.Int(args, "--variants", 1);
        var brief = ArgReader.String(args, "--brief", string.Empty);
        var output = ArgReader.String(args, "--out", file);

        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);
        if (target == GenerationTarget.Knowledge && args.Contains("--replace"))
        {
            project.Knowledge.Clear();
        }

        var applied = await CreateBuilder(context).GenerateIntoAsync(project, target, brief, variants, cancellationToken);
        await SaveAsync(project, output, cancellationToken);
        Console.WriteLine($"Applied {applied} option(s).");
        return 0;
    }

    private static async Task<int> SetupAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var output = ArgReader.String(args, "--out", "examples/story.story.json");
        var brief = ArgReader.String(args, "--brief", string.Empty);
        var characters = ArgReader.Int(args, "--characters", 3);

        using var context = await BuildAsync(args, cancellationToken);
        var project = await CreateBuilder(context).CreateSetupAsync(brief, characters, cancellationToken);
        await SaveAsync(project, output, cancellationToken);
        return 0;
    }

    private static async Task<int> WriteAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev write --file <path> [--chapters N]");
            return 2;
        }

        var count = ArgReader.Int(args, "--chapters", 1);
        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);
        await CreateBuilder(context).WriteChaptersAsync(project, count, cancellationToken);
        await SaveAsync(project, file, cancellationToken);
        return 0;
    }

    private static async Task<int> SummarizeAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev summarize --file <path> [--chapter N | --all] [--out <path>]");
            return 2;
        }

        var all = args.Contains("--all");
        var number = ArgReader.Int(args, "--chapter", 1);
        var output = ArgReader.String(args, "--out", file);

        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);
        var summarizer = new ChapterSummarizer(context.LlmClient, context.SettingsService);

        var chapters = all
            ? project.Chapters.ToList()
            : project.Chapters.Where(chapter => chapter.Number == number).ToList();

        if (chapters.Count == 0)
        {
            Console.Error.WriteLine($"chapter {number} not found");
            return 2;
        }

        foreach (var chapter in chapters)
        {
            if (string.IsNullOrWhiteSpace(chapter.ContentOriginal))
            {
                Console.WriteLine($"  chapter {chapter.Number}: empty, skipped");
                continue;
            }

            var index = project.Chapters.IndexOf(chapter);
            var stateBefore = index > 0 ? project.Chapters[index - 1].WorldState ?? project.InitialWorldState : project.InitialWorldState;
            var knowledge = KnowledgeComposer.Compose(project, chapter.Number);
            Console.WriteLine($"  Summarizing chapter {chapter.Number}…");
            var summary = await summarizer.SummarizeAsync(chapter, stateBefore, knowledge, new ConsoleProgress($"chapter {chapter.Number}"), cancellationToken);
            chapter.Logline = summary.Logline;
            chapter.WorldState = summary.WorldState;
            chapter.KnowledgeChanges = [.. summary.KnowledgeChanges];
            Console.WriteLine($"  chapter {chapter.Number} logline: {summary.Logline}");
            Console.WriteLine($"  chapter {chapter.Number} state: {summary.WorldState.TimeAndPlace}");
        }

        await SaveAsync(project, output, cancellationToken);
        return 0;
    }

    private static async Task<int> ExportAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev export --file <path> [--language <code>] [--out <path.fb2>]");
            return 2;
        }

        var language = ArgReader.String(args, "--language", "en");
        var output = ArgReader.String(args, "--out", Path.ChangeExtension(file, ".fb2"));

        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);
        var fb2 = Fb2Exporter.Build(project, language);
        await File.WriteAllTextAsync(output, fb2, new UTF8Encoding(false), cancellationToken);

        Console.WriteLine($"Exported ({language}) -> {Path.GetFullPath(output)}");
        return 0;
    }

    private static async Task<int> DesignAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        var prompt = ArgReader.Value(args, "--prompt");
        if (string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(prompt))
        {
            Console.Error.WriteLine("usage: storydev design --file <path> --prompt <text> [--out <path>]");
            return 2;
        }

        var output = ArgReader.String(args, "--out", file);

        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);
        var importer = new KnowledgeImporter(context.LlmClient, context.SettingsService);

        if (importer.Plan(prompt).TooLarge)
        {
            Console.Error.WriteLine("prompt is too large to process in one import");
            return 2;
        }

        var entries = await importer.ExtractAsync(
            new KnowledgeImportRequest(prompt, string.Empty, Mode: KnowledgeImportMode.Design),
            progress: null,
            cancellationToken);

        foreach (var entry in entries)
        {
            project.Knowledge.Add(entry);
            Console.WriteLine($"  [{entry.Kind}] {entry.Title}");
        }

        Console.WriteLine($"  {entries.Count} entr(ies) added");
        await SaveAsync(project, output, cancellationToken);
        return 0;
    }

    private static async Task<int> TranslateAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev translate --file <path> --language <code> [--out <path>]");
            return 2;
        }

        var language = ArgReader.String(args, "--language", "ru");
        var output = ArgReader.String(args, "--out", file);

        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);
        var translator = new TranslationService(context.LlmClient, context.SettingsService);

        foreach (var chapter in project.Chapters.Where(chapter => !string.IsNullOrWhiteSpace(chapter.ContentOriginal)))
        {
            Console.WriteLine($"  Translating chapter {chapter.Number} -> {language}…");
            var text = await translator.TranslateAsync(chapter.ContentOriginal, language, new ConsoleProgress("translate"), cancellationToken);
            chapter.Translations[language] = text;
            chapter.StaleTranslations.Remove(language);
            Console.WriteLine($"    {text.Length} chars");
        }

        await SaveAsync(project, output, cancellationToken);
        return 0;
    }

    private static async Task<int> ContextAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev context --file <path> --number N");
            return 2;
        }

        var number = ArgReader.Int(args, "--number", 1);
        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);
        var chapter = project.Chapters.FirstOrDefault(candidate => candidate.Number == number);
        if (chapter is null)
        {
            Console.Error.WriteLine($"chapter {number} not found");
            return 2;
        }

        var index = project.Chapters.IndexOf(chapter);
        var stateBefore = index > 0 ? project.Chapters[index - 1].WorldState ?? project.InitialWorldState : project.InitialWorldState;
        var settings = await context.SettingsService.LoadAsync(cancellationToken);
        var writerContext = new WriterContext(
            project,
            chapter,
            stateBefore,
            settings.ContextTokenBudget,
            settings.RecentLoglineCount,
            settings.ContextRequiredSectionMaxChars);
        var assembled = new ChapterContextAssembler().AssembleWriter(writerContext);

        Console.WriteLine($"  estimated tokens: ~{assembled.EstimatedTokens}");
        foreach (var message in assembled.Messages)
        {
            Console.WriteLine();
            Console.WriteLine($"===== {message.Role} =====");
            Console.WriteLine(message.Content);
        }

        return 0;
    }

    private static async Task<int> RegenerateAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev regenerate --file <path> --chapter N [--out <path>]");
            return 2;
        }

        var number = ArgReader.Int(args, "--chapter", 1);
        var output = ArgReader.String(args, "--out", file);

        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);
        var chapter = project.Chapters.FirstOrDefault(candidate => candidate.Number == number);
        if (chapter is null)
        {
            Console.Error.WriteLine($"chapter {number} not found");
            return 2;
        }

        Console.WriteLine($"  Regenerating chapter {number}…");
        var runner = CreateRunner(context);
        await runner.GenerateAsync(project, chapter, new ConsoleProgress($"chapter {number}"), cancellationToken);
        Console.WriteLine($"  Chapter {number}: {chapter.ContentOriginal.Length} chars");
        Console.WriteLine($"    logline: {chapter.Logline}");
        await SaveAsync(project, output, cancellationToken);
        return 0;
    }

    private static async Task<int> JudgeAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev judge --file <path> --chapter N");
            return 2;
        }

        var number = ArgReader.Int(args, "--chapter", 1);
        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);
        var chapter = project.Chapters.FirstOrDefault(candidate => candidate.Number == number);
        if (chapter is null || string.IsNullOrWhiteSpace(chapter.ContentOriginal))
        {
            Console.Error.WriteLine($"chapter {number} not found or empty");
            return 2;
        }

        var index = project.Chapters.IndexOf(chapter);
        var stateBefore = index > 0 ? project.Chapters[index - 1].WorldState ?? project.InitialWorldState : project.InitialWorldState;
        var settings = await context.SettingsService.LoadAsync(cancellationToken);
        var storySoFar = PromptTemplates.WriterRecap(project, chapter, settings.RecentLoglineCount);
        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = PromptTemplates.BuildContinuityJudge(storySoFar, stateBefore, chapter),
            Temperature = 0,
            MaxTokens = settings.MaxTokens,
        };

        var json = await context.LlmClient
            .CompleteJsonAsync(context.Connection, request, "ChapterContinuity", ChapterContinuitySchema.Build(), cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine(json.Trim());
        await AppendLogAsync(
            ArgReader.Value(args, "--log"),
            "judge",
            new JsonObject { ["file"] = file, ["chapter"] = number },
            json,
            cancellationToken);
        return 0;
    }

    private static async Task<int> CompareAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var fileA = ArgReader.Value(args, "--a");
        var fileB = ArgReader.Value(args, "--b");
        if (string.IsNullOrWhiteSpace(fileA) || string.IsNullOrWhiteSpace(fileB))
        {
            Console.Error.WriteLine("usage: storydev compare --a <pathA> --b <pathB> --chapter N");
            return 2;
        }

        var number = ArgReader.Int(args, "--chapter", 1);
        using var context = await BuildAsync(args, cancellationToken);
        var repository = new JsonProjectRepository();
        var projectA = await repository.LoadAsync(fileA, cancellationToken);
        var projectB = await repository.LoadAsync(fileB, cancellationToken);
        var chapterA = projectA.Chapters.FirstOrDefault(candidate => candidate.Number == number);
        var chapterB = projectB.Chapters.FirstOrDefault(candidate => candidate.Number == number);
        if (chapterA is null || chapterB is null
            || string.IsNullOrWhiteSpace(chapterA.ContentOriginal) || string.IsNullOrWhiteSpace(chapterB.ContentOriginal))
        {
            Console.Error.WriteLine($"chapter {number} not found or empty in one of the files");
            return 2;
        }

        var index = projectA.Chapters.IndexOf(chapterA);
        var stateBefore = index > 0 ? projectA.Chapters[index - 1].WorldState ?? projectA.InitialWorldState : projectA.InitialWorldState;
        var settings = await context.SettingsService.LoadAsync(cancellationToken);
        var storySoFar = PromptTemplates.WriterRecap(projectA, chapterA, settings.RecentLoglineCount);
        var request = new LlmRequest
        {
            Model = settings.Model,
            Messages = PromptTemplates.BuildContinuityComparison(storySoFar, stateBefore, number, chapterA.ContentOriginal, chapterB.ContentOriginal),
            Temperature = 0,
            MaxTokens = settings.MaxTokens,
        };

        var json = await context.LlmClient
            .CompleteJsonAsync(context.Connection, request, "ChapterContinuityComparison", ChapterContinuityComparisonSchema.Build(), cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine(json.Trim());
        await AppendLogAsync(
            ArgReader.Value(args, "--log"),
            "compare",
            new JsonObject { ["a"] = fileA, ["b"] = fileB, ["chapter"] = number },
            json,
            cancellationToken);
        return 0;
    }

    private static async Task AppendLogAsync(string? path, string command, JsonObject meta, string json, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        JsonNode? result;
        try
        {
            result = JsonNode.Parse(json);
        }
        catch (System.Text.Json.JsonException)
        {
            result = json;
        }

        var record = new JsonObject
        {
            ["ts"] = DateTimeOffset.UtcNow.ToString("O"),
            ["command"] = command,
            ["meta"] = meta,
            ["result"] = result,
        };

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.AppendAllTextAsync(path, record.ToJsonString() + Environment.NewLine, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> CreateAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var output = ArgReader.String(args, "--out", "examples/story.story.json");
        var brief = ArgReader.String(args, "--brief", string.Empty);
        var characters = ArgReader.Int(args, "--characters", 3);
        var chapters = ArgReader.Int(args, "--chapters", 2);

        using var context = await BuildAsync(args, cancellationToken);
        var builder = CreateBuilder(context);
        var project = await builder.CreateSetupAsync(brief, characters, cancellationToken);
        await builder.WriteChaptersAsync(project, chapters, cancellationToken);
        await SaveAsync(project, output, cancellationToken);
        return 0;
    }

    private static BookBuilder CreateBuilder(CliContext context) =>
        new(new GenerationAssistant(context.LlmClient, context.SettingsService), CreateRunner(context), new SystemClock());

    private static IChapterRunner CreateRunner(CliContext context) => CreateRunner(context.LlmClient, context.SettingsService);

    private static IChapterRunner CreateRunner(ILlmClient llmClient, ISettingsService settingsService)
    {
        var writer = new ChapterAgent(llmClient, settingsService, new ChapterContextAssembler());
        var editor = new ChapterEditor(llmClient, settingsService, new DiffPlexTextDiff());
        var summarizer = new ChapterSummarizer(llmClient, settingsService);
        var workflow = new ChapterWorkflow(writer, editor, summarizer, settingsService);
        return new ChapterRunner(workflow, summarizer, new SystemClock());
    }

    private static async Task<int> ExperimentAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev experiment --file <base.story.json> [--hypotheses Both,Retelling,Loglines] [--from N] [--to M] [--out <dir>]");
            return 2;
        }

        var output = ArgReader.String(args, "--out", "hypotheses");
        var hypotheses = ArgReader.String(args, "--hypotheses", "Both")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var from = ArgReader.Int(args, "--from", 1);

        var baseSettings = await LoadSettingsAsync(args, cancellationToken);
        var httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var repository = new JsonProjectRepository();

        foreach (var hypothesis in hypotheses)
        {
            var directory = Path.Combine(output, SanitizeName(hypothesis));
            var promptDirectory = Path.Combine(directory, "prompts");
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            Directory.CreateDirectory(promptDirectory);

            var settings = CloneSettings(baseSettings);
            var settingsService = new CliSettingsService(settings);
            var tracing = new TracingLlmClient(new OpenAiCompatibleLlmClient(httpClient), promptDirectory);
            var runner = CreateRunner(tracing, settingsService);

            var project = await repository.LoadAsync(file, cancellationToken);
            var to = ArgReader.Int(args, "--to", project.Chapters.Count);
            var log = new List<string> { $"hypothesis: {hypothesis}", $"range: {from}..{to}", $"model: {settings.Model}" };

            for (var number = from; number <= to; number++)
            {
                var chapter = project.Chapters.FirstOrDefault(candidate => candidate.Number == number);
                if (chapter is null)
                {
                    continue;
                }

                Console.WriteLine($"  [{hypothesis}] chapter {number}…");
                await runner.GenerateAsync(project, chapter, new ConsoleProgress($"ch{number}"), cancellationToken);
                log.Add($"chapter {number}: {chapter.Logline}");
            }

            await SaveAsync(project, Path.Combine(directory, "book.story.json"), cancellationToken);
            await File.WriteAllLinesAsync(Path.Combine(directory, "run.log"), log, cancellationToken);

            var meta = new JsonObject
            {
                ["hypothesis"] = hypothesis,
                ["from"] = from,
                ["to"] = to,
                ["contextTokenBudget"] = settings.ContextTokenBudget,
                ["recentLoglineCount"] = settings.RecentLoglineCount,
                ["contextRequiredSectionMaxChars"] = settings.ContextRequiredSectionMaxChars,
                ["toolResultMaxChars"] = settings.ToolResultMaxChars,
                ["model"] = settings.Model,
                ["temperature"] = settings.Temperature,
            };
            await File.WriteAllTextAsync(Path.Combine(directory, "meta.json"), meta.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        }

        Console.WriteLine($"Saved experiments under {Path.GetFullPath(output)}");
        return 0;
    }

    private static AppSettings CloneSettings(AppSettings source) => new()
    {
        SchemaVersion = source.SchemaVersion,
        Provider = source.Provider,
        BaseUrl = source.BaseUrl,
        Model = source.Model,
        ApiKey = source.ApiKey,
        TimeoutSeconds = source.TimeoutSeconds,
        MaxTokens = source.MaxTokens,
        MaxToolCalls = source.MaxToolCalls,
        ContextTokenBudget = source.ContextTokenBudget,
        RecentLoglineCount = source.RecentLoglineCount,
        ContextRequiredSectionMaxChars = source.ContextRequiredSectionMaxChars,
        ToolResultMaxChars = source.ToolResultMaxChars,
        Temperature = source.Temperature,
        DefaultLanguageCode = source.DefaultLanguageCode,
        Languages = source.Languages,
        RecentProjects = source.RecentProjects,
    };

    private static string SanitizeName(string value) =>
        string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));

    private static async Task<int> ReviewAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev review --file <path> [--brief <text>] [--trace <dir>] [--out <path.json>]");
            return 2;
        }

        var brief = ArgReader.String(args, "--brief", string.Empty);
        var trace = ArgReader.Value(args, "--trace");
        var output = ArgReader.Value(args, "--out");

        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);

        ILlmClient client = context.LlmClient;
        if (!string.IsNullOrWhiteSpace(trace))
        {
            client = new TracingLlmClient(client, trace);
        }

        var reviewer = new ProjectReviewAssistant(client, context.SettingsService);
        var findings = await reviewer.ReviewAsync(project, brief, new ConsoleProgress("review"), cancellationToken);

        foreach (var finding in findings)
        {
            Console.WriteLine($"  [{finding.Severity}] ({finding.Area}) {finding.Title}");
            Console.WriteLine($"      {finding.Detail}");
            if (!string.IsNullOrWhiteSpace(finding.Reference))
            {
                Console.WriteLine($"      reference: {finding.Reference}");
            }

            if (!string.IsNullOrWhiteSpace(finding.Suggestion))
            {
                Console.WriteLine($"      suggestion: {finding.Suggestion}");
            }

            if (finding.Fix is { IsEmpty: false } fix)
            {
                foreach (var edit in fix.Edits)
                {
                    Console.WriteLine($"      fix: {edit.Target}.{edit.Field} [{edit.Reference}] = {edit.Value}");
                }
            }
        }

        Console.WriteLine($"  {findings.Count} finding(s)");

        if (!string.IsNullOrWhiteSpace(output))
        {
            await File.WriteAllTextAsync(
                output,
                FindingsToJson(findings).ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            Console.WriteLine($"  written: {Path.GetFullPath(output)}");
        }

        return 0;
    }

    private static JsonArray FindingsToJson(IReadOnlyList<ReviewFinding> findings) =>
        new([.. findings.Select(finding => (JsonNode)new JsonObject
        {
            ["severity"] = finding.Severity.ToString(),
            ["area"] = finding.Area.ToString(),
            ["title"] = finding.Title,
            ["detail"] = finding.Detail,
            ["suggestion"] = finding.Suggestion,
            ["reference"] = finding.Reference,
            ["fix"] = finding.Fix is null
                ? null
                : new JsonObject
                {
                    ["edits"] = new JsonArray([.. finding.Fix.Edits.Select(edit => (JsonNode)new JsonObject
                    {
                        ["target"] = edit.Target.ToString(),
                        ["reference"] = edit.Reference,
                        ["field"] = edit.Field,
                        ["value"] = edit.Value,
                    })]),
                },
        })]);

    private static async Task<int> ContinuityAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev continuity --file <path> [--chapter N | --all]");
            return 2;
        }

        var all = args.Contains("--all");
        var number = ArgReader.Int(args, "--chapter", 1);

        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);
        var reviewer = new ContinuityReviewer(context.LlmClient, context.SettingsService);

        var chapters = all
            ? project.Chapters.Where(chapter => !string.IsNullOrWhiteSpace(chapter.Direction)).ToList()
            : project.Chapters.Where(chapter => chapter.Number == number).ToList();

        var total = 0;
        foreach (var chapter in chapters)
        {
            Console.WriteLine($"  Chapter {chapter.Number}…");
            var findings = await reviewer.ReviewAsync(project, chapter, cancellationToken);
            foreach (var finding in findings)
            {
                total++;
                Console.WriteLine($"    [{finding.Severity}] {finding.Title} ({finding.Reference})");
                Console.WriteLine($"        {finding.Detail}");
            }

            if (findings.Count == 0)
            {
                Console.WriteLine("    no contradictions found");
            }
        }

        Console.WriteLine($"  {total} finding(s)");
        return 0;
    }

    private static async Task<int> RecomputeAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev recompute --file <path> [--from N] [--out <path>]");
            return 2;
        }

        var from = ArgReader.Int(args, "--from", 1);
        var output = ArgReader.String(args, "--out", file);

        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);
        Console.WriteLine($"  Recomputing summaries from chapter {from}…");
        await CreateRunner(context).RecomputeFromAsync(project, from, new ConsoleProgress("recompute"), cancellationToken);
        await SaveAsync(project, output, cancellationToken);
        return 0;
    }

    private static async Task SaveAsync(Project project, string path, CancellationToken cancellationToken)
    {
        await new JsonProjectRepository().SaveAsync(project, path, cancellationToken);
        Console.WriteLine($"Saved: {Path.GetFullPath(path)}");
        Console.WriteLine($"  {project.Name} · {project.Knowledge.Count(entry => entry.Kind == KnowledgeKind.Character)} character(s) · {project.Chapters.Count} chapter(s)");
    }

    private static string Detail(string? detail) => string.IsNullOrWhiteSpace(detail) ? string.Empty : $" ({detail})";

    private static int Help()
    {
        Console.WriteLine("storydev — developer CLI for the StoryTelling engine");
        Console.WriteLine();
        Console.WriteLine("Usage: storydev <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  ping      Check provider connectivity and structured-output support");
        Console.WriteLine("  gen       Generate one target into a project: --target <Target> --file <path> [--variants N] [--replace]");
        Console.WriteLine("  setup     Generate project setup: --out <path> [--brief ...] [--characters N]");
        Console.WriteLine("  write     Write chapters into an existing project: --file <path> [--chapters N]");
        Console.WriteLine("  create    Full run (setup + chapters): --out <path> [--chapters N] [--brief ...] [--characters N]");
        Console.WriteLine("  summarize Summarize chapters into loglines + world state: --file <path> [--chapter N | --all]");
        Console.WriteLine("  recompute Refresh summaries/world state from a chapter onward: --file <path> [--from N]");
        Console.WriteLine("  translate Translate chapters into a language: --file <path> --language <code>");
        Console.WriteLine("  design    Build knowledge from a description: --file <path> --prompt <text>");
        Console.WriteLine("  export    Write an FB2: --file <path> [--language <code>] [--out <path.fb2>] (no provider needed)");
        Console.WriteLine("  context   Print the assembled writer prompt for one chapter (no provider): --file <path> --number N");
        Console.WriteLine("  regenerate Rewrite one chapter in place with the current seed: --file <path> --chapter N [--out <path>]");
        Console.WriteLine("  continuity Check a chapter's direction against the established facts: --file <path> [--chapter N | --all]");
        Console.WriteLine("  review    AI-review the knowledge base for contradictions: --file <path> [--brief <text>] [--trace <dir>] [--out <path.json>]");
        Console.WriteLine("  judge     Score whether a chapter continues the story: --file <path> --chapter N");
        Console.WriteLine("  compare   Pick which of two chapter drafts continues better: --a <pathA> --b <pathB> --chapter N");
        Console.WriteLine("  experiment Run full-book passes with full prompt traces: --file <base> [--out <dir>] [--from N] [--to M]");
        Console.WriteLine();
        Console.WriteLine("Common options:");
        Console.WriteLine("  --base-url --model --api-key --max-tokens --max-tool-calls --temperature --timeout");
        Console.WriteLine("  --context-token-budget --recent-loglines --required-cap --tool-result-max-chars");
        Console.WriteLine("  --log <path>   Append judge/compare verdicts as JSON lines (experiment results)");
        return 0;
    }
}

