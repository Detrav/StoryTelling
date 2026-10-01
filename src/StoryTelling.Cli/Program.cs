using System.Text;
using StoryTelling.Application.Chapters;
using StoryTelling.Application.Export;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Knowledge;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
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
                "raw" => await RawAsync(rest, token),
                "probe" => await ProbeAsync(rest, token),
                "gen" => await GenAsync(rest, token),
                "setup" => await SetupAsync(rest, token),
                "write" => await WriteAsync(rest, token),
                "summarize" => await SummarizeAsync(rest, token),
                "translate" => await TranslateAsync(rest, token),
                "design" => await DesignAsync(rest, token),
                "export" => await ExportAsync(rest, token),
                "recompute" => await RecomputeAsync(rest, token),
                "edit" => await EditAsync(rest, token),
                "draft" => await DraftAsync(rest, token),
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

    private static async Task<int> RawAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var prompt = ArgReader.Value(args, "--prompt");
        if (string.IsNullOrWhiteSpace(prompt))
        {
            Console.Error.WriteLine("usage: storydev raw --prompt <text> [--system <text>]");
            return 2;
        }

        using var context = await BuildAsync(args, cancellationToken);
        var messages = new List<LlmMessage>();
        if (ArgReader.Value(args, "--system") is { } system)
        {
            messages.Add(LlmMessage.System(system));
        }

        messages.Add(LlmMessage.User(prompt));

        var completion = await context.LlmClient.CompleteAsync(context.Connection, new LlmRequest
        {
            Model = context.Settings.Model,
            Messages = messages,
            Temperature = ArgReader.Double(args, "--temperature", context.Settings.Temperature),
            MaxTokens = ArgReader.Int(args, "--max-tokens", context.Settings.MaxTokens),
        }, cancellationToken);

        Console.WriteLine(completion.Content);
        return 0;
    }

    private static async Task<int> ProbeAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var targetName = ArgReader.Value(args, "--target");
        if (targetName is null || !Enum.TryParse<GenerationTarget>(targetName, ignoreCase: true, out var target))
        {
            Console.Error.WriteLine("usage: storydev probe --target <Target> [--variants N] [--file <path>] [--brief ...] [--out <path>]");
            return 2;
        }

        var variants = ArgReader.Int(args, "--variants", 1);
        var brief = ArgReader.String(args, "--brief", string.Empty);
        var file = ArgReader.Value(args, "--file");

        using var context = await BuildAsync(args, cancellationToken);
        var project = file is null
            ? new Project { Name = "Probe" }
            : await new JsonProjectRepository().LoadAsync(file, cancellationToken);

        var request = new GenerationRequest
        {
            Target = target,
            Brief = brief,
            Variants = variants,
            Context = BookBuilder.BuildContext(project),
        };

        var llmRequest = new LlmRequest
        {
            Model = context.Settings.Model,
            Messages = PromptTemplates.Build(request, useTools: false),
            Temperature = context.Settings.Temperature,
            MaxTokens = context.Settings.MaxTokens,
        };

        var raw = await context.LlmClient.CompleteJsonAsync(
            context.Connection,
            llmRequest,
            GenerationTargets.SchemaName(target),
            GenerationTargets.BuildSchema(target, variants),
            cancellationToken);

        var output = ArgReader.String(args, "--out", $"examples/probe-{target}.json");
        var directory = Path.GetDirectoryName(Path.GetFullPath(output));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(output, raw, cancellationToken);

        Console.WriteLine(raw);
        Console.WriteLine();
        Console.WriteLine($"Saved raw response: {Path.GetFullPath(output)}");
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

    private static async Task<int> EditAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev edit --file <path> --number N [--in <draft.txt>] [--out <edited.txt>]");
            return 2;
        }

        var number = ArgReader.Int(args, "--number", 1);
        var input = ArgReader.Value(args, "--in");
        var output = ArgReader.String(args, "--out", $"examples/dbg/ch{number}-edited.txt");

        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);
        var chapter = project.Chapters.FirstOrDefault(candidate => candidate.Number == number);
        if (chapter is null)
        {
            Console.Error.WriteLine($"chapter {number} not found");
            return 2;
        }

        var draft = input is not null ? await File.ReadAllTextAsync(input, cancellationToken) : chapter.ContentOriginal;
        var index = project.Chapters.IndexOf(chapter);
        var stateBefore = index > 0 ? project.Chapters[index - 1].WorldState ?? project.InitialWorldState : project.InitialWorldState;

        Console.WriteLine($"  Editing chapter {number} (draft {draft.Length} chars)…");
        var edited = await new ChapterEditor(context.LlmClient, context.SettingsService, new DiffPlexTextDiff())
            .EditAsync(project, chapter, draft, stateBefore, new ConsoleProgress("edit"), cancellationToken);

        await WriteAndReportAsync(output, edited.Text, cancellationToken);
        Console.WriteLine($"  editor notes: {edited.Notes.Count}");
        return 0;
    }

    private static async Task<int> DraftAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev draft --file <path> --number N [--out <draft.txt>]");
            return 2;
        }

        var number = ArgReader.Int(args, "--number", 1);
        var output = ArgReader.String(args, "--out", $"examples/dbg/ch{number}-draft.txt");

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
        Console.WriteLine($"  Drafting chapter {number}…");
        var draft = await new ChapterAgent(context.LlmClient, context.SettingsService, new ChapterContextAssembler())
            .WriteAsync(new WriterContext(project, chapter, stateBefore, ChapterContextAssembler.DefaultTokenBudget), new ConsoleProgress("draft"), null, cancellationToken);

        await WriteAndReportAsync(output, draft.Text, cancellationToken);
        return 0;
    }

    private static async Task WriteAndReportAsync(string path, string text, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, text, cancellationToken);
        Console.WriteLine($"  result: {text.Length} chars -> {Path.GetFullPath(path)}");
        Console.WriteLine("  tail: " + text[Math.Max(0, text.Length - 160)..].Replace('\n', ' '));
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

    private static IChapterRunner CreateRunner(CliContext context)
    {
        var writer = new ChapterAgent(context.LlmClient, context.SettingsService, new ChapterContextAssembler());
        var editor = new ChapterEditor(context.LlmClient, context.SettingsService, new DiffPlexTextDiff());
        var summarizer = new ChapterSummarizer(context.LlmClient, context.SettingsService);
        var workflow = new ChapterWorkflow(writer, editor, summarizer);
        return new ChapterRunner(workflow, summarizer, new SystemClock());
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
        Console.WriteLine("  raw       Send a raw prompt: --prompt <text> [--system <text>]");
        Console.WriteLine("  probe     Raw schema output for one target: --target <Target> [--variants N] [--file <path>]");
        Console.WriteLine("  gen       Generate one target into a project: --target <Target> --file <path> [--variants N] [--replace]");
        Console.WriteLine("  setup     Generate project setup: --out <path> [--brief ...] [--characters N]");
        Console.WriteLine("  write     Write chapters into an existing project: --file <path> [--chapters N]");
        Console.WriteLine("  summarize Summarize chapters into loglines + world state: --file <path> [--chapter N | --all]");
        Console.WriteLine("  translate Translate chapters into a language: --file <path> --language <code>");
        Console.WriteLine("  design    Build knowledge from a description: --file <path> --prompt <text>");
        Console.WriteLine("  recompute Refresh summaries/world state from a chapter onward: --file <path> [--from N]");
        Console.WriteLine("  edit      Run the editor on one chapter (debug): --file <path> --number N [--in <draft.txt>] [--out <edited.txt>]");
        Console.WriteLine("  draft     Run the writer on one chapter (debug): --file <path> --number N [--out <draft.txt>]");
        Console.WriteLine("  create    Full run (setup + chapters): --out <path> [--chapters N] [--brief ...] [--characters N]");
        Console.WriteLine("  export    Write an FB2: --file <path> [--language <code>] [--out <path.fb2>] (no provider needed)");
        Console.WriteLine();
        Console.WriteLine("Common options:");
        Console.WriteLine("  --base-url --model --api-key --max-tokens --max-tool-calls --temperature --timeout");
        return 0;
    }
}
