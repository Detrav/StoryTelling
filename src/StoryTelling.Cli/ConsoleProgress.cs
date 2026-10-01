using StoryTelling.Application.Generation;

namespace StoryTelling.Cli;

internal sealed class ConsoleProgress : IProgress<GenerationProgress>
{
    private readonly string _label;

    public ConsoleProgress(string label) => _label = label;

    public void Report(GenerationProgress value)
    {
        var tools = value.ToolCalls > 0 ? $" ({value.ToolCalls} tool calls)" : string.Empty;
        Console.WriteLine($"    [{_label}] {value.Stage}{tools}");
    }
}
