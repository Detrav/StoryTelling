using StoryTelling.Application.Generation;

namespace StoryTelling.Application.Llm;

public enum LlmTask
{
    Writer,
    Editor,
    EditorChecker,
    EditorFixer,
    EditorCosmetic,
    StyleRepair,
    Setup,
    Planner,
    Import,
    Summarizer,
    Translation,
    Review,
    Continuity,
}

public static class LlmTasks
{
    public static IReadOnlyList<LlmTask> All { get; } = Enum.GetValues<LlmTask>();

    public static IReadOnlyList<string> ReasoningEffortLevels { get; } = ["none", "minimal", "low", "medium", "high"];

    public static bool IsKnownReasoningEffort(string value) =>
        string.IsNullOrWhiteSpace(value) || ReasoningEffortLevels.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string Id(this LlmTask task) => task switch
    {
        LlmTask.Writer => "writer",
        LlmTask.Editor => "editor",
        LlmTask.EditorChecker => "editorChecker",
        LlmTask.EditorFixer => "editorFixer",
        LlmTask.EditorCosmetic => "editorCosmetic",
        LlmTask.StyleRepair => "styleRepair",
        LlmTask.Setup => "setup",
        LlmTask.Planner => "planner",
        LlmTask.Import => "import",
        LlmTask.Summarizer => "summarizer",
        LlmTask.Translation => "translation",
        LlmTask.Review => "review",
        LlmTask.Continuity => "continuity",
        _ => task.ToString(),
    };

    public static string Label(this LlmTask task) => task switch
    {
        LlmTask.Writer => "Chapter writer",
        LlmTask.Editor => "Editor",
        LlmTask.EditorChecker => "Editor checker (ok/not ok)",
        LlmTask.EditorFixer => "Editor fixer (per axis)",
        LlmTask.EditorCosmetic => "Editor cosmetic (prose polish)",
        LlmTask.StyleRepair => "Style repair",
        LlmTask.Setup => "Setup (world, cast, knowledge)",
        LlmTask.Planner => "Chapter planner",
        LlmTask.Import => "Knowledge import",
        LlmTask.Summarizer => "Chapter summarizer",
        LlmTask.Translation => "Translation",
        LlmTask.Review => "Knowledge review",
        LlmTask.Continuity => "Continuity check",
        _ => task.ToString(),
    };

    public static double DefaultTemperature(this LlmTask task) => task switch
    {
        LlmTask.Writer or LlmTask.Editor or LlmTask.EditorFixer or LlmTask.EditorCosmetic or LlmTask.Setup or LlmTask.Planner => 0.8,
        LlmTask.EditorChecker or LlmTask.StyleRepair or LlmTask.Import or LlmTask.Summarizer => 0.3,
        LlmTask.Translation or LlmTask.Review or LlmTask.Continuity => 0.2,
        _ => 0.2,
    };

    public static bool FollowsGlobalTemperature(this LlmTask task) =>
        task is LlmTask.Writer or LlmTask.Editor or LlmTask.EditorFixer or LlmTask.EditorCosmetic or LlmTask.Setup or LlmTask.Planner;

    public static string DefaultReasoningEffort(this LlmTask task) => task switch
    {
        LlmTask.Review => "none",
        _ => string.Empty,
    };

    public static LlmTask ForGenerationTarget(GenerationTarget target) => target switch
    {
        GenerationTarget.ChapterSettings or GenerationTarget.ChapterPlan or GenerationTarget.Finale => LlmTask.Planner,
        _ => LlmTask.Setup,
    };
}
