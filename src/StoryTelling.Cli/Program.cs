using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
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
using StoryTelling.Infrastructure.Logging;

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
                "plan" => await PlanAsync(rest, token),
                "complete" => await CompleteAsync(rest, token),
                "finish" => await FinishAsync(rest, token),
                "chapter" => await ChapterAsync(rest, token),
                "set" => await SetAsync(rest, token),
                "settings" => await SettingsAsync(rest, token),
                "import" => await ImportAsync(rest, token),
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
                "apply-fixes" => await ApplyFixesAsync(rest, token),
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
        var level = ResolveLogLevel(args);
        var (loggerFactory, logPath) = CreateLoggerFactory(level);
        var httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var llmClient = new OpenAiCompatibleLlmClient(httpClient, loggerFactory.CreateLogger<OpenAiCompatibleLlmClient>());
        loggerFactory.CreateLogger("storydev").LogInformation("storydev {Command} ({LogLevel})", string.Join(' ', args), level);
        return new CliContext(settings, httpClient, llmClient, new CliSettingsService(settings), loggerFactory, logPath);
    }

    private static LogLevel ResolveLogLevel(IReadOnlyList<string> args)
    {
        if (args.Contains("--verbose"))
        {
            return LogLevel.Debug;
        }

        var value = ArgReader.Value(args, "--log-level");
        return value is not null && Enum.TryParse<LogLevel>(value, ignoreCase: true, out var level)
            ? level
            : LogLevel.Information;
    }

    private static (ILoggerFactory Factory, string Path) CreateLoggerFactory(LogLevel level)
    {
        try
        {
            AppPaths.PruneOldLogs();
            var path = AppPaths.CreateLogFilePath();
            var factory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(level);
                builder.AddProvider(new FileLoggerProvider(path, level));
            });

            return (factory, path);
        }
        catch (Exception)
        {
            return (LoggerFactory.Create(builder => builder.SetMinimumLevel(level)), AppPaths.LogsDirectory);
        }
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

    private static async Task<int> PlanAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev plan --file <path> [--chapters N] [--brief ...] [--out <path>] [--replace]");
            return 2;
        }

        var count = Math.Clamp(ArgReader.Int(args, "--chapters", 10), 1, 50);
        var brief = ArgReader.String(args, "--brief", string.Empty);
        var output = ArgReader.String(args, "--out", file);

        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);

        if (project.Chapters.Any(chapter => !string.IsNullOrWhiteSpace(chapter.ContentOriginal)) && !args.Contains("--replace"))
        {
            Console.Error.WriteLine("the project already has written chapters; pass --replace to discard them");
            return 2;
        }

        var snapshot = WithKnowledge(project, KnowledgeComposer.Compose(project, 1));
        var request = new GenerationRequest
        {
            Target = GenerationTarget.ChapterPlan,
            Brief = brief,
            Variants = count,
            Context = BookBuilder.BuildContext(project),
            Snapshot = snapshot,
        };

        var assistant = new GenerationAssistant(context.LlmClient, context.SettingsService);
        var options = await assistant.GenerateAsync(request, new GenerationSession(), new ConsoleProgress("planning"), cancellationToken);

        var chapters = new List<Chapter>();
        foreach (var option in options)
        {
            var title = option.Fields.TryGetValue("Title", out var candidateTitle) ? candidateTitle.Trim() : string.Empty;
            var direction = option.Fields.TryGetValue("Direction", out var candidateDirection) ? candidateDirection.Trim() : string.Empty;
            if (title.Length == 0 && direction.Length == 0)
            {
                continue;
            }

            var chapter = new Chapter
            {
                Number = chapters.Count + 1,
                Title = title.Length == 0 ? $"Chapter {chapters.Count + 1}" : title,
                Direction = direction,
                Status = ChapterStatus.Draft,
                CreatedUtc = DateTimeOffset.UtcNow,
            };

            foreach (var code in project.Settings.TargetLanguages)
            {
                chapter.Translations[code] = string.Empty;
            }

            chapters.Add(chapter);
        }

        if (chapters.Count == 0)
        {
            Console.Error.WriteLine("the model returned no usable plan; try again");
            return 1;
        }

        project.Chapters = chapters;
        project.UpdatedUtc = DateTimeOffset.UtcNow;
        await SaveAsync(project, output, cancellationToken);
        Console.WriteLine($"Planned {chapters.Count} chapter(s).");
        return 0;
    }

    private static async Task<int> CompleteAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev complete --file <path> [--languages <codes>] [--no-translate] [--out <path>]");
            return 2;
        }

        var output = ArgReader.String(args, "--out", file);
        var noTranslate = args.Contains("--no-translate");

        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);
        var runner = CreateRunner(context);
        var logger = context.LoggerFactory.CreateLogger("complete");

        var languages = noTranslate
            ? []
            : (ArgReader.Value(args, "--languages")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
               ?? [.. project.Settings.TargetLanguages]);

        foreach (var chapter in project.Chapters.OrderBy(chapter => chapter.Number))
        {
            var needsText = string.IsNullOrWhiteSpace(chapter.ContentOriginal) || chapter.Status == ChapterStatus.Stale;
            var needsSummary = !string.IsNullOrWhiteSpace(chapter.ContentOriginal)
                && (string.IsNullOrWhiteSpace(chapter.Logline) || chapter.WorldState is null);

            if (needsText)
            {
                if (MissingForGeneration(project, chapter) is { } reason)
                {
                    Console.WriteLine($"  Chapter {chapter.Number}: skipped ({reason})");
                    continue;
                }

                Console.WriteLine($"  Writing chapter {chapter.Number}…");
                await runner.GenerateAsync(project, chapter, new ConsoleProgress($"chapter {chapter.Number}"), cancellationToken);
                Console.WriteLine($"  Chapter {chapter.Number}: {chapter.ContentOriginal.Length} chars");
            }
            else if (needsSummary)
            {
                Console.WriteLine($"  Summarizing chapter {chapter.Number}…");
                await runner.RegenerateSummaryAsync(project, chapter, new ConsoleProgress($"chapter {chapter.Number}"), cancellationToken);
            }

            foreach (var code in languages)
            {
                if (string.IsNullOrWhiteSpace(chapter.ContentOriginal))
                {
                    continue;
                }

                var stale = chapter.StaleTranslations.Contains(code) || string.IsNullOrWhiteSpace(chapter.Translations.GetValueOrDefault(code));
                if (!stale)
                {
                    continue;
                }

                Console.WriteLine($"  Translating chapter {chapter.Number} -> {code}…");
                var service = new TranslationService(context.LlmClient, context.SettingsService);
                chapter.Translations[code] = await service.TranslateAsync(chapter.ContentOriginal, code, new ConsoleProgress($"chapter {chapter.Number} [{code}]"), cancellationToken);
                chapter.StaleTranslations.Remove(code);
            }
        }

        project.UpdatedUtc = DateTimeOffset.UtcNow;
        await SaveAsync(project, output, cancellationToken);
        logger.LogInformation("complete finished: {Chapters} chapter(s)", project.Chapters.Count);
        return 0;
    }

    private static string? MissingForGeneration(Project project, Chapter chapter)
    {
        if (string.IsNullOrWhiteSpace(project.World.Genre) && string.IsNullOrWhiteSpace(project.World.Body))
        {
            return "no world set up";
        }

        if (string.IsNullOrWhiteSpace(project.InitialWorldState.TimeAndPlace) && string.IsNullOrWhiteSpace(project.InitialWorldState.Situation))
        {
            return "no initial world state";
        }

        if (string.IsNullOrWhiteSpace(chapter.Direction))
        {
            return "no direction";
        }

        return null;
    }

    private static async Task<int> FinishAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file))
        {
            Console.Error.WriteLine("usage: storydev finish --file <path> [--brief ...] [--out <path>]");
            return 2;
        }

        var brief = ArgReader.String(args, "--brief", string.Empty);
        var output = ArgReader.String(args, "--out", file);

        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);

        var number = project.Chapters.Count == 0 ? 1 : project.Chapters.Max(chapter => chapter.Number) + 1;
        var snapshot = WithKnowledge(project, KnowledgeComposer.Compose(project, number), project.Chapters);
        var request = new GenerationRequest
        {
            Target = GenerationTarget.Finale,
            Brief = brief,
            Variants = 1,
            Context = BookBuilder.BuildContext(project),
            Snapshot = snapshot,
        };

        var assistant = new GenerationAssistant(context.LlmClient, context.SettingsService);
        var options = await assistant.GenerateAsync(request, new GenerationSession(), new ConsoleProgress("finale"), cancellationToken);
        var option = options.FirstOrDefault();
        if (option is null)
        {
            Console.Error.WriteLine("the model returned no usable final chapter; try again");
            return 1;
        }

        var title = option.Fields.TryGetValue("Title", out var candidateTitle) ? candidateTitle.Trim() : string.Empty;
        var direction = option.Fields.TryGetValue("Direction", out var candidateDirection) ? candidateDirection.Trim() : string.Empty;
        var chapter = new Chapter
        {
            Number = number,
            Title = title.Length == 0 ? "Finale" : title,
            Direction = direction,
            Role = ChapterRole.Finale,
            Status = ChapterStatus.Draft,
            CreatedUtc = DateTimeOffset.UtcNow,
        };

        foreach (var code in project.Settings.TargetLanguages)
        {
            chapter.Translations[code] = string.Empty;
        }

        project.Chapters.Add(chapter);
        project.UpdatedUtc = DateTimeOffset.UtcNow;
        await SaveAsync(project, output, cancellationToken);
        Console.WriteLine($"Planned final chapter {number}: {chapter.Title}");
        Console.WriteLine($"  {chapter.Direction}");
        return 0;
    }

    private static Project WithKnowledge(Project project, IReadOnlyList<KnowledgeEntry> knowledge, IReadOnlyList<Chapter>? chapters = null) => new()
    {
        SchemaVersion = project.SchemaVersion,
        Id = project.Id,
        Name = project.Name,
        CreatedUtc = project.CreatedUtc,
        UpdatedUtc = project.UpdatedUtc,
        Settings = project.Settings,
        World = project.World,
        Knowledge = [.. knowledge],
        Chapters = [.. chapters ?? project.Chapters],
        InitialWorldState = project.InitialWorldState,
    };

    private static async Task<int> ChapterAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var action = ArgReader.Value(args, "--action") ?? args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal));
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(action))
        {
            Console.Error.WriteLine("usage: storydev chapter --action <add|remove|move|status> --file <path> [--number N] [--from N] [--status Draft|Generated|Stale] [--out <path>]");
            return 2;
        }

        var output = ArgReader.String(args, "--out", file);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);

        switch (action.ToLowerInvariant())
        {
            case "add":
                var number = project.Chapters.Count == 0 ? 1 : project.Chapters.Max(chapter => chapter.Number) + 1;
                var added = new Chapter { Number = number, Title = $"Chapter {number}", Status = ChapterStatus.Draft, CreatedUtc = DateTimeOffset.UtcNow };
                foreach (var code in project.Settings.TargetLanguages)
                {
                    added.Translations[code] = string.Empty;
                }

                project.Chapters.Add(added);
                Console.WriteLine($"Added chapter {number}.");
                break;

            case "remove":
                var target = FindChapter(project, ArgReader.Int(args, "--number", 1));
                if (target is null)
                {
                    return ChapterNotFound(args);
                }

                project.Chapters.Remove(target);
                Renumber(project);
                Console.WriteLine($"Removed chapter {target.Number}.");
                break;

            case "move":
                var moving = FindChapter(project, ArgReader.Int(args, "--number", 1));
                if (moving is null)
                {
                    return ChapterNotFound(args);
                }

                var index = project.Chapters.IndexOf(moving);
                var destination = Math.Clamp(ArgReader.Int(args, "--from", index), 0, project.Chapters.Count - 1);
                project.Chapters.RemoveAt(index);
                project.Chapters.Insert(destination, moving);
                Renumber(project);
                Console.WriteLine($"Moved chapter to position {destination + 1}.");
                break;

            case "status":
                var statusTarget = FindChapter(project, ArgReader.Int(args, "--number", 1));
                if (statusTarget is null)
                {
                    return ChapterNotFound(args);
                }

                if (!Enum.TryParse<ChapterStatus>(ArgReader.String(args, "--status", "Draft"), ignoreCase: true, out var status))
                {
                    Console.Error.WriteLine("--status must be Draft, Generated or Stale");
                    return 2;
                }

                statusTarget.Status = status;
                Console.WriteLine($"Chapter {statusTarget.Number} -> {status}.");
                break;

            default:
                Console.Error.WriteLine($"unknown action '{action}'");
                return 2;
        }

        project.UpdatedUtc = DateTimeOffset.UtcNow;
        await SaveAsync(project, output, cancellationToken);
        return 0;
    }

    private static int ChapterNotFound(IReadOnlyList<string> args)
    {
        Console.Error.WriteLine($"chapter {ArgReader.Int(args, "--number", 1)} not found");
        return 2;
    }

    private static Chapter? FindChapter(Project project, int number) =>
        project.Chapters.FirstOrDefault(chapter => chapter.Number == number);

    private static void Renumber(Project project)
    {
        var ordered = project.Chapters.OrderBy(chapter => chapter.Number).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Number = i + 1;
        }

        project.Chapters = ordered;
    }

    private static async Task<int> SetAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var kind = ArgReader.Value(args, "--what") ?? args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal));
        var file = ArgReader.Value(args, "--file");
        if (string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(kind))
        {
            Console.Error.WriteLine("usage: storydev set --what <world|state|chapter|knowledge|project> --file <path> [field options] [--out <path>]");
            return 2;
        }

        var output = ArgReader.String(args, "--out", file);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);

        switch (kind.ToLowerInvariant())
        {
            case "project":
                project.Name = SetString(args, "--name", project.Name);
                break;

            case "world":
                project.World.Title = SetString(args, "--title", project.World.Title);
                project.World.Body = SetString(args, "--body", project.World.Body);
                project.World.Genre = SetString(args, "--genre", project.World.Genre);
                project.World.Tone = SetString(args, "--tone", project.World.Tone);
                project.World.Style = SetString(args, "--style", project.World.Style);
                project.World.PointOfView = SetString(args, "--pov", project.World.PointOfView);
                project.World.Tense = SetString(args, "--tense", project.World.Tense);
                project.World.Rating = SetString(args, "--rating", project.World.Rating);
                break;

            case "state":
                project.InitialWorldState.TimeAndPlace = SetString(args, "--time-and-place", project.InitialWorldState.TimeAndPlace);
                project.InitialWorldState.Situation = SetString(args, "--situation", project.InitialWorldState.Situation);
                break;

            case "chapter":
                var chapter = FindChapter(project, ArgReader.Int(args, "--number", 1));
                if (chapter is null)
                {
                    return ChapterNotFound(args);
                }

                chapter.Title = SetString(args, "--title", chapter.Title);
                chapter.Direction = SetString(args, "--direction", chapter.Direction);
                chapter.Notes = SetString(args, "--notes", chapter.Notes);
                chapter.Logline = SetString(args, "--logline", chapter.Logline);
                if (ArgReader.Value(args, "--role") is { } roleName && Enum.TryParse<ChapterRole>(roleName, ignoreCase: true, out var role))
                {
                    chapter.Role = role;
                }

                if (ArgReader.Value(args, "--text-file") is { } textFile)
                {
                    chapter.ContentOriginal = await File.ReadAllTextAsync(textFile, cancellationToken);
                }
                else
                {
                    chapter.ContentOriginal = SetString(args, "--text", chapter.ContentOriginal);
                }

                if (ArgReader.Value(args, "--situation") is { } situation)
                {
                    chapter.WorldState ??= new WorldState();
                    chapter.WorldState.Situation = situation;
                }

                if (ArgReader.Value(args, "--time-and-place") is { } where)
                {
                    chapter.WorldState ??= new WorldState();
                    chapter.WorldState.TimeAndPlace = where;
                }

                break;

            case "knowledge":
                var title = ArgReader.Value(args, "--entry");
                var entry = project.Knowledge.FirstOrDefault(candidate => string.Equals(candidate.Title, title, StringComparison.OrdinalIgnoreCase));
                if (entry is null)
                {
                    Console.Error.WriteLine($"knowledge entry '{title}' not found");
                    return 2;
                }

                if (ArgReader.Value(args, "--kind") is { } entryKind && Enum.TryParse<KnowledgeKind>(entryKind, ignoreCase: true, out var parsedKind))
                {
                    entry.Kind = parsedKind;
                }

                entry.Title = SetString(args, "--title", entry.Title);
                entry.Content = SetString(args, "--content", entry.Content);
                if (ArgReader.Value(args, "--tags") is { } tags)
                {
                    entry.Tags = [.. tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
                }

                if (ArgReader.Value(args, "--status") is { } statusName && Enum.TryParse<KnowledgeStatus>(statusName, ignoreCase: true, out var parsedStatus))
                {
                    entry.Status = parsedStatus;
                }

                break;

            default:
                Console.Error.WriteLine($"unknown --what '{kind}'");
                return 2;
        }

        project.UpdatedUtc = DateTimeOffset.UtcNow;
        await SaveAsync(project, output, cancellationToken);
        Console.WriteLine("Saved.");
        return 0;
    }

    private static string SetString(IReadOnlyList<string> args, string name, string current) =>
        ArgReader.Value(args, name) ?? current;

    private static async Task<int> SettingsAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var action = ArgReader.Value(args, "--action") ?? args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal)) ?? "show";
        var service = new JsonSettingsService(AppPaths.SettingsFile);

        if (action.Equals("show", StringComparison.OrdinalIgnoreCase))
        {
            var current = await service.LoadAsync(cancellationToken);
            Console.WriteLine($"provider:   {current.Provider}");
            Console.WriteLine($"base url:   {current.BaseUrl}");
            Console.WriteLine($"model:      {current.Model}");
            Console.WriteLine($"api key:    {(string.IsNullOrWhiteSpace(current.ApiKey) ? "(not set)" : "(set)")}");
            Console.WriteLine($"timeout:    {current.TimeoutSeconds}s");
            Console.WriteLine($"max tokens: {current.MaxTokens}");
            Console.WriteLine($"temp:       {current.Temperature}");
            Console.WriteLine($"languages:  {string.Join(", ", current.Languages.Select(language => language.Code))}");
            Console.WriteLine($"file:       {AppPaths.SettingsFile}");
            return 0;
        }

        if (!action.Equals("set", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("usage: storydev settings [show|set] [--base-url ...] [--model ...] [--api-key ...] [--temperature ...] [--languages ru,de]");
            return 2;
        }

        var settings = await service.LoadAsync(cancellationToken);
        settings.BaseUrl = ArgReader.String(args, "--base-url", settings.BaseUrl);
        settings.Model = ArgReader.String(args, "--model", settings.Model);
        settings.ApiKey = ArgReader.String(args, "--api-key", settings.ApiKey);
        settings.Provider = ArgReader.String(args, "--provider", settings.Provider);
        settings.TimeoutSeconds = ArgReader.Int(args, "--timeout", settings.TimeoutSeconds);
        settings.MaxTokens = ArgReader.Int(args, "--max-tokens", settings.MaxTokens);
        settings.MaxToolCalls = ArgReader.Int(args, "--max-tool-calls", settings.MaxToolCalls);
        settings.Temperature = ArgReader.Double(args, "--temperature", settings.Temperature);
        settings.ContextTokenBudget = ArgReader.Int(args, "--context-token-budget", settings.ContextTokenBudget);
        settings.RecentLoglineCount = ArgReader.Int(args, "--recent-loglines", settings.RecentLoglineCount);
        settings.ContextRequiredSectionMaxChars = ArgReader.Int(args, "--required-cap", settings.ContextRequiredSectionMaxChars);
        settings.ToolResultMaxChars = ArgReader.Int(args, "--tool-result-max-chars", settings.ToolResultMaxChars);
        if (ArgReader.Value(args, "--languages") is { } languages)
        {
            settings.Languages =
            [
                .. languages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(code => new LanguageData(code, code.ToUpperInvariant())),
            ];
        }

        await service.SaveAsync(settings, cancellationToken);
        Console.WriteLine($"Saved settings to {AppPaths.SettingsFile}.");
        return 0;
    }

    private static async Task<int> ImportAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        var source = ArgReader.Value(args, "--from");
        if (string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(source))
        {
            Console.Error.WriteLine("usage: storydev import --file <path> --from <file.md|dir> [--brief ...] [--mode extract|design] [--out <path>]");
            return 2;
        }

        var output = ArgReader.String(args, "--out", file);
        var brief = ArgReader.String(args, "--brief", string.Empty);
        var mode = ArgReader.String(args, "--mode", "extract").Equals("design", StringComparison.OrdinalIgnoreCase)
            ? KnowledgeImportMode.Design
            : KnowledgeImportMode.Extract;

        var files = Directory.Exists(source)
            ? Directory.GetFiles(source, "*.md", SearchOption.AllDirectories)
            : [source];
        if (files.Length == 0)
        {
            Console.Error.WriteLine($"no files found at {source}");
            return 2;
        }

        var content = string.Join("\n\n", await Task.WhenAll(files.Select(path => File.ReadAllTextAsync(path, cancellationToken))));

        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);
        var importer = new KnowledgeImporter(context.LlmClient, context.SettingsService);
        var entries = await importer.ExtractAsync(new KnowledgeImportRequest(content, brief, KnowledgeImportRequest.DefaultMaxChunks, mode), null, cancellationToken);

        var seen = new HashSet<string>(project.Knowledge.Select(entry => entry.Title), StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var entry in entries)
        {
            if (seen.Add(entry.Title))
            {
                project.Knowledge.Add(entry);
                added++;
            }
        }

        project.UpdatedUtc = DateTimeOffset.UtcNow;
        await SaveAsync(project, output, cancellationToken);
        Console.WriteLine($"Imported {added} new knowledge entr{(added == 1 ? "y" : "ies")} from {files.Length} file(s).");
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
            Console.Error.WriteLine("usage: storydev translate --file <path> --language <code> [--metadata-only] [--with-metadata] [--out <path>]");
            return 2;
        }

        var language = ArgReader.String(args, "--language", "ru");
        var output = ArgReader.String(args, "--out", file);
        var metadataOnly = args.Contains("--metadata") || args.Contains("--metadata-only");

        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);

        if (!metadataOnly)
        {
            var translator = new TranslationService(context.LlmClient, context.SettingsService);
            foreach (var chapter in project.Chapters.Where(chapter => !string.IsNullOrWhiteSpace(chapter.ContentOriginal)))
            {
                Console.WriteLine($"  Translating chapter {chapter.Number} -> {language}…");
                var text = await translator.TranslateAsync(chapter.ContentOriginal, language, new ConsoleProgress("translate"), cancellationToken);
                chapter.Translations[language] = text;
                chapter.StaleTranslations.Remove(language);
                Console.WriteLine($"    {text.Length} chars");
            }
        }

        if (metadataOnly || args.Contains("--with-metadata") || args.Contains("--metadata"))
        {
            await TranslateMetadataAsync(context, project, language, cancellationToken);
        }

        await SaveAsync(project, output, cancellationToken);
        return 0;
    }

    private static async Task TranslateMetadataAsync(CliContext context, Project project, string language, CancellationToken cancellationToken)
    {
        var titles = project.Chapters
            .Where(chapter => !string.IsNullOrWhiteSpace(chapter.Title))
            .Select(chapter => new MetadataChapterTitle(chapter.Number, chapter.Title))
            .ToList();
        var request = new MetadataTranslationRequest(language, project.Name, project.World?.Body ?? string.Empty, titles);
        var translator = new MetadataTranslationService(context.LlmClient, context.SettingsService);
        var result = await translator.TranslateAsync(request, new ConsoleProgress("metadata"), cancellationToken);

        if (!project.MetadataTranslations.TryGetValue(language, out var existing))
        {
            existing = new MetadataTranslation();
            project.MetadataTranslations[language] = existing;
        }

        existing.Name = result.BookName;
        existing.Annotation = result.Annotation;
        foreach (var chapter in project.Chapters)
        {
            if (result.ChapterTitles.TryGetValue(chapter.Number, out var title) && !string.IsNullOrWhiteSpace(title))
            {
                chapter.TranslatedTitles[language] = title;
            }
        }

        var coverage = MetadataTranslationCoverage.Evaluate(project, language);
        if (coverage.IsComplete)
        {
            project.StaleMetadataTranslations.RemoveAll(code => string.Equals(code, language, StringComparison.OrdinalIgnoreCase));
        }
        else if (!project.StaleMetadataTranslations.Contains(language))
        {
            project.StaleMetadataTranslations.Add(language);
        }

        Console.WriteLine($"  Metadata -> {language}: '{result.BookName}', {result.ChapterTitles.Count} chapter title(s)");
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

        using var context = await BuildAsync(args, cancellationToken);
        var logger = context.LoggerFactory.CreateLogger("experiment");
        var repository = new JsonProjectRepository();

        foreach (var hypothesis in hypotheses)
        {
            var directory = Path.Combine(output, SanitizeName(hypothesis));
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            Directory.CreateDirectory(directory);

            var settings = CloneSettings(context.Settings);
            var settingsService = new CliSettingsService(settings);
            var runner = CreateRunner(context.LlmClient, settingsService);

            var project = await repository.LoadAsync(file, cancellationToken);
            var to = ArgReader.Int(args, "--to", project.Chapters.Count);
            logger.LogInformation(
                "experiment {Hypothesis}: range {From}..{To}, model {Model}, context budget {Budget}, recent loglines {Recent}, required cap {Cap}, tool result cap {ToolCap}, temperature {Temperature}",
                hypothesis,
                from,
                to,
                settings.Model,
                settings.ContextTokenBudget,
                settings.RecentLoglineCount,
                settings.ContextRequiredSectionMaxChars,
                settings.ToolResultMaxChars,
                settings.Temperature);

            for (var number = from; number <= to; number++)
            {
                var chapter = project.Chapters.FirstOrDefault(candidate => candidate.Number == number);
                if (chapter is null)
                {
                    continue;
                }

                Console.WriteLine($"  [{hypothesis}] chapter {number}…");
                await runner.GenerateAsync(project, chapter, new ConsoleProgress($"ch{number}"), cancellationToken);
                logger.LogInformation("experiment {Hypothesis} chapter {Number}: {Logline}", hypothesis, number, chapter.Logline);
            }

            await SaveAsync(project, Path.Combine(directory, "book.story.json"), cancellationToken);
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
            Console.Error.WriteLine("usage: storydev review --file <path> [--check numbers,facts|all] [--brief <text>] [--out <path.json>] [--apply] [--apply-content]");
            return 2;
        }

        var brief = ArgReader.String(args, "--brief", string.Empty);
        var output = ArgReader.Value(args, "--out");
        var requested = ArgReader.Value(args, "--check");
        var apply = args.Contains("--apply");
        var applyContent = args.Contains("--apply-content");

        var checks = SelectChecks(requested);
        if (checks.Count == 0)
        {
            Console.Error.WriteLine($"unknown --check value(s): {requested}");
            return 2;
        }

        using var context = await BuildAsync(args, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);

        var reviewer = new ProjectReviewAssistant(context.LlmClient, context.SettingsService);
        var all = new List<ReviewFinding>();

        foreach (var check in checks)
        {
            Console.WriteLine($"== {check.Label} ({check.Id}) ==");

            IReadOnlyList<ReviewFinding> findings;
            try
            {
                findings = await reviewer.ReviewAsync(project, brief, check, new ConsoleProgress("review"), cancellationToken);
            }
            catch (LlmException exception)
            {
                Console.WriteLine($"  failed ({exception.Kind}): {exception.Message}");
                continue;
            }

            all.AddRange(findings);

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
        }

        Console.WriteLine($"  {all.Count} finding(s) total");

        if (apply)
        {
            var applied = 0;
            foreach (var finding in all)
            {
                if (finding.Fix is { IsEmpty: false } fix && ApplyReviewFix(project, fix, applyContent))
                {
                    applied++;
                }
            }

            await SaveAsync(project, file, cancellationToken);
            Console.WriteLine($"  applied {applied} fix(es) to {Path.GetFullPath(file)}");
        }

        if (!string.IsNullOrWhiteSpace(output))
        {
            await File.WriteAllTextAsync(
                output,
                FindingsToJson(all).ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            Console.WriteLine($"  written: {Path.GetFullPath(output)}");
        }

        return 0;
    }

    private static async Task<int> ApplyFixesAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var file = ArgReader.Value(args, "--file");
        var fixesPath = ArgReader.Value(args, "--fixes");
        if (string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(fixesPath))
        {
            Console.Error.WriteLine("usage: storydev apply-fixes --file <book.json> --fixes <edits.json>");
            return 2;
        }

        var json = await File.ReadAllTextAsync(fixesPath, cancellationToken);
        var project = await new JsonProjectRepository().LoadAsync(file, cancellationToken);

        var applied = 0;
        var total = 0;
        using (var document = JsonDocument.Parse(json))
        {
            foreach (var element in document.RootElement.EnumerateArray())
            {
                total++;
                if (element.TryGetProperty("create", out var create) && create.ValueKind == JsonValueKind.Object)
                {
                    CreateKnowledgeEntry(project, create);
                    applied++;
                    continue;
                }

                if (element.TryGetProperty("section", out _))
                {
                    applied += ApplySectionEdit(project, element) ? 1 : 0;
                    continue;
                }

                var entry = project.Knowledge.FirstOrDefault(candidate =>
                    string.Equals(candidate.Title.Trim(), GetJsonString(element, "reference").Trim(), StringComparison.OrdinalIgnoreCase));
                if (entry is null)
                {
                    Console.WriteLine($"  not resolved: {GetJsonString(element, "reference")}");
                    continue;
                }

                if (element.TryGetProperty("find", out _))
                {
                    var find = GetJsonString(element, "find");
                    var replace = GetJsonString(element, "replace");
                    if (find.Length > 0 && entry.Content.Contains(find, StringComparison.Ordinal))
                    {
                        entry.Content = entry.Content.Replace(find, replace, StringComparison.Ordinal);
                        applied++;
                    }
                    else
                    {
                        Console.WriteLine($"  find not present in [{entry.Title}]: {find}");
                    }

                    continue;
                }

                if (ApplyCuratedEdit(project, new ReviewEdit(GenerationTarget.Knowledge, entry.Title, GetJsonString(element, "field"), GetJsonString(element, "value"))))
                {
                    applied++;
                }
                else
                {
                    Console.WriteLine($"  not applied: {GetJsonString(element, "field")} [{entry.Title}]");
                }
            }
        }

        await SaveAsync(project, file, cancellationToken);
        Console.WriteLine($"  applied {applied}/{total} edit(s)");
        return 0;
    }

    private static bool ApplySectionEdit(Project project, JsonElement element)
    {
        var field = GetJsonString(element, "field");
        var find = GetJsonString(element, "find");
        var replace = GetJsonString(element, "replace");
        var value = GetJsonString(element, "value");

        string? Apply(string? current)
        {
            if (current is null)
            {
                return null;
            }

            if (find.Length > 0)
            {
                return current.Contains(find, StringComparison.Ordinal) ? current.Replace(find, replace, StringComparison.Ordinal) : null;
            }

            return value;
        }

        switch (GetJsonString(element, "section").ToLowerInvariant())
        {
            case "world":
                return ApplyWorld(project, field, Apply);
            case "initialworldstate":
                return ApplyInitialState(project, field, Apply);
            default:
                return false;
        }
    }

    private static bool ApplyWorld(Project project, string field, Func<string?, string?> apply)
    {
        var world = project.World;
        switch (field.Trim().ToLowerInvariant())
        {
            case "title": return Set(world.Title, apply, value => world.Title = value);
            case "body": return Set(world.Body, apply, value => world.Body = value);
            case "genre": return Set(world.Genre, apply, value => world.Genre = value);
            case "tone": return Set(world.Tone, apply, value => world.Tone = value);
            case "style": return Set(world.Style, apply, value => world.Style = value);
            case "pointofview": return Set(world.PointOfView, apply, value => world.PointOfView = value);
            case "tense": return Set(world.Tense, apply, value => world.Tense = value);
            case "rating": return Set(world.Rating, apply, value => world.Rating = value);
            default: return false;
        }
    }

    private static bool ApplyInitialState(Project project, string field, Func<string?, string?> apply)
    {
        var state = project.InitialWorldState;
        return field.Trim().ToLowerInvariant() switch
        {
            "timeandplace" => Set(state.TimeAndPlace, apply, value => state.TimeAndPlace = value),
            "situation" => Set(state.Situation, apply, value => state.Situation = value),
            _ => false,
        };
    }

    private static bool Set(string current, Func<string?, string?> apply, Action<string> assign)
    {
        if (apply(current) is { } updated)
        {
            assign(updated);
            return true;
        }

        return false;
    }

    private static void CreateKnowledgeEntry(Project project, JsonElement create)
    {
        var entry = new KnowledgeEntry
        {
            Kind = Enum.TryParse<KnowledgeKind>(GetJsonString(create, "kind"), ignoreCase: true, out var kind) ? kind : KnowledgeKind.Note,
            Title = GetJsonString(create, "title").Trim(),
            Content = GetJsonString(create, "content"),
        };

        if (create.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
        {
            entry.Tags = [.. tags.EnumerateArray().Where(tag => tag.ValueKind == JsonValueKind.String).Select(tag => tag.GetString()!.Trim()).Where(tag => tag.Length > 0)];
        }

        project.Knowledge.Add(entry);
    }

    private static bool ApplyCuratedEdit(Project project, ReviewEdit edit)
    {
        var entry = project.Knowledge.FirstOrDefault(candidate =>
            string.Equals(candidate.Title.Trim(), edit.Reference.Trim(), StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return false;
        }

        switch (edit.Field.Trim().ToLowerInvariant())
        {
            case "kind" when Enum.TryParse<KnowledgeKind>(edit.Value.Trim(), ignoreCase: true, out var kind):
                entry.Kind = kind;
                return true;
            case "title":
                entry.Title = edit.Value.Trim();
                return true;
            case "tags":
                entry.Tags = [.. edit.Value.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
                return true;
            case "content":
                entry.Content = edit.Value;
                return true;
            default:
                return false;
        }
    }

    private static string GetJsonString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static bool ApplyReviewFix(Project project, ReviewFix fix, bool applyContent)
    {
        var changed = false;
        foreach (var edit in fix.Edits)
        {
            if (edit.Target != GenerationTarget.Knowledge)
            {
                continue;
            }

            var entry = project.Knowledge.FirstOrDefault(candidate =>
                string.Equals(candidate.Title.Trim(), edit.Reference.Trim(), StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                continue;
            }

            switch (edit.Field.Trim().ToLowerInvariant())
            {
                case "kind" when Enum.TryParse<KnowledgeKind>(edit.Value.Trim(), ignoreCase: true, out var kind):
                    entry.Kind = kind;
                    break;
                case "title":
                    Console.WriteLine($"      skip rename [{entry.Title}] -> [{edit.Value.Trim()}]");
                    continue;
                case "tags":
                    entry.Tags = [.. edit.Value.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
                    break;
                case "content":
                    if (!applyContent)
                    {
                        Console.WriteLine($"      skip content (use --apply-content) [{entry.Title}]");
                        continue;
                    }

                    var value = edit.Value.Trim();
                    if (value.Length < entry.Content.Length * 0.8)
                    {
                        Console.WriteLine($"      skip destructive content shrink [{entry.Title}] ({entry.Content.Length} -> {value.Length} chars)");
                        continue;
                    }

                    entry.Content = edit.Value;
                    break;
                default:
                    continue;
            }

            changed = true;
        }

        return changed;
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

    private static IReadOnlyList<ReviewCheck> SelectChecks(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested) || string.Equals(requested, "all", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewChecks.ForScope(ReviewScope.Project);
        }

        var result = new List<ReviewCheck>();
        foreach (var id in requested.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (ReviewChecks.Find(id) is { Scope: ReviewScope.Project } check)
            {
                result.Add(check);
            }
        }

        return result;
    }

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
        Console.WriteLine("  plan      Plan the whole book as N chapters (title + direction): --file <path> [--chapters N] [--brief ...] [--replace]");
        Console.WriteLine("  complete  Finish the whole book (write/summarize/translate pending work): --file <path> [--languages ru,de] [--no-translate]");
        Console.WriteLine("  finish    Plan the final chapter (title + direction): --file <path> [--brief ...]");
        Console.WriteLine("  chapter   Manage chapters: --action <add|remove|move|status> --file <path> [--number N] [--from N] [--status ...]");
        Console.WriteLine("  set       Edit fields by hand: --what <project|world|state|chapter|knowledge> --file <path> [field options]");
        Console.WriteLine("  settings  Show or set provider settings: [show|set] [--model ...] [--base-url ...] [--api-key ...] [--languages ru,de]");
        Console.WriteLine("  import    Import knowledge from Markdown files: --file <path> --from <file.md|dir> [--mode extract|design] [--brief ...]");
        Console.WriteLine("  create    Full run (setup + chapters): --out <path> [--chapters N] [--brief ...] [--characters N]");
        Console.WriteLine("  summarize Summarize chapters into loglines + world state: --file <path> [--chapter N | --all]");
        Console.WriteLine("  recompute Refresh summaries/world state from a chapter onward: --file <path> [--from N]");
        Console.WriteLine("  translate Translate chapters and/or metadata: --file <path> --language <code> [--metadata-only] [--with-metadata]");
        Console.WriteLine("  design    Build knowledge from a description: --file <path> --prompt <text>");
        Console.WriteLine("  export    Write an FB2: --file <path> [--language <code>] [--out <path.fb2>] (no provider needed)");
        Console.WriteLine("  context   Print the assembled writer prompt for one chapter (no provider): --file <path> --number N");
        Console.WriteLine("  regenerate Rewrite one chapter in place with the current seed: --file <path> --chapter N [--out <path>]");
        Console.WriteLine("  continuity Check a chapter's direction against the established facts: --file <path> [--chapter N | --all]");
        Console.WriteLine("  review    AI-review the knowledge base for contradictions: --file <path> [--check numbers,facts|all] [--brief <text>] [--out <path.json>] [--apply] [--apply-content]");
        Console.WriteLine("  apply-fixes Apply curated knowledge edits: --file <book.json> --fixes <edits.json>");
        Console.WriteLine("  judge     Score whether a chapter continues the story: --file <path> --chapter N");
        Console.WriteLine("  compare   Pick which of two chapter drafts continues better: --a <pathA> --b <pathB> --chapter N");
        Console.WriteLine("  experiment Run full-book passes per hypothesis: --file <base> [--out <dir>] [--from N] [--to M]");
        Console.WriteLine();
        Console.WriteLine("Common options:");
        Console.WriteLine("  --base-url --model --api-key --max-tokens --max-tool-calls --temperature --timeout");
        Console.WriteLine("  --context-token-budget --recent-loglines --required-cap --tool-result-max-chars");
        Console.WriteLine($"  --log-level <level>  File log verbosity: Trace|Debug|Information|Warning|Error (default Information; log at {AppPaths.LogsDirectory})");
        Console.WriteLine("  --verbose            Alias for --log-level Debug (logs full API requests/responses)");
        return 0;
    }
}

