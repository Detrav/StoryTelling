using System.Text;
using StoryTelling.Application.Generation;
using StoryTelling.Application.Llm;

namespace StoryTelling.Application.Prompts;

public static class PromptTemplates
{
    private static readonly (string Field, string Label)[] _contextFields =
    [
        ("ProjectName", "Book name"),
        ("WorldTitle", "World title"),
        ("WorldBody", "World description"),
        ("Genre", "Genre"),
        ("Tone", "Tone"),
        ("Premise", "Premise"),
        ("Direction", "Direction"),
    ];

    public static IReadOnlyList<LlmMessage> Build(GenerationRequest request)
    {
        var system = "You help outline a multi-chapter story. Work in English only. "
            + $"Reply with exactly {request.Variants} distinct options that match the required JSON schema — "
            + "no prose, no explanations.";

        var ownFields = GenerationTargets.Fields(request.Target).Select(spec => spec.Field).ToHashSet();
        var user = new StringBuilder();
        user.AppendLine("Current project:");

        var wroteConstraint = false;
        var wroteDraft = false;
        foreach (var (field, label) in _contextFields)
        {
            var value = ValueOf(request.Context, field);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (ownFields.Contains(field))
            {
                if (!wroteDraft)
                {
                    user.AppendLine();
                    user.AppendLine("Current draft (improve it or replace it entirely):");
                    wroteDraft = true;
                }

                user.AppendLine($"- {label}: {value.Trim()}");
            }
            else
            {
                wroteConstraint = true;
                user.AppendLine($"- {label}: {value.Trim()}");
            }
        }

        if (!wroteConstraint && !wroteDraft)
        {
            user.AppendLine("- (nothing yet)");
        }

        user.AppendLine();
        user.AppendLine(GenerationTargets.Instruction(request.Target));
        if (!string.IsNullOrWhiteSpace(request.Brief))
        {
            user.AppendLine($"Author's brief: {request.Brief.Trim()}");
        }

        user.AppendLine($"Provide exactly {request.Variants} distinct options.");

        return [LlmMessage.System(system), LlmMessage.User(user.ToString())];
    }

    private static string ValueOf(GenerationContext context, string field) => field switch
    {
        "ProjectName" => context.ProjectName,
        "WorldTitle" => context.WorldTitle,
        "WorldBody" => context.WorldBody,
        "Genre" => context.Genre,
        "Tone" => context.Tone,
        "Premise" => context.Premise,
        "Direction" => context.Direction,
        _ => string.Empty,
    };
}
