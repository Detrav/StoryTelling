using StoryTelling.Application.Chapters;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;
using StoryTelling.Application.Prompts;
using StoryTelling.Application.Settings;
using StoryTelling.Domain;
using StoryTelling.Infrastructure;
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
            MaxTokens = 16,
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
        if (target == GenerationTarget.Character && args.Contains("--replace"))
        {
            project.Characters.Clear();
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

    private static BookBuilder CreateBuilder(CliContext context)
    {
        var assistant = new GenerationAssistant(context.LlmClient, context.SettingsService);
        var chapterAgent = new ChapterAgent(context.LlmClient, context.SettingsService, new ChapterContextAssembler());
        return new BookBuilder(assistant, chapterAgent, new SystemClock());
    }

    private static async Task SaveAsync(Project project, string path, CancellationToken cancellationToken)
    {
        await new JsonProjectRepository().SaveAsync(project, path, cancellationToken);
        Console.WriteLine($"Saved: {Path.GetFullPath(path)}");
        Console.WriteLine($"  {project.Name} · {project.Characters.Count} character(s) · {project.Chapters.Count} chapter(s)");
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
        Console.WriteLine("  create    Full run (setup + chapters): --out <path> [--chapters N] [--brief ...] [--characters N]");
        Console.WriteLine();
        Console.WriteLine("Common options:");
        Console.WriteLine("  --base-url --model --api-key --max-tokens --max-tool-calls --temperature --timeout");
        return 0;
    }
}
