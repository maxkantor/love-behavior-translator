using System.Text;
using System.Text.RegularExpressions;

namespace LoveBehaviorTranslator.Function;

public static class PromptFactory
{
    private const string BasePrompt =
        "You are a compassionate relationship advisor and behavioral analyst.\n" +
        "Analyze the described romantic or interpersonal behavior.\n\n" +
        "Explain:\n" +
        "1. What this behavior might mean emotionally\n" +
        "2. Common communication or psychological reasons\n" +
        "3. Whether this behavior is a red flag, neutral, or positive\n" +
        "4. Practical advice on how to respond calmly and constructively\n\n" +
        "Be supportive, non-judgmental, and realistic.\n" +
        "Do not give medical or psychological diagnoses.\n" +
        "Do not shame or blame either person.";

    private static readonly Dictionary<string, string> ModeVariants = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gentle"] =
            "Use a warm, empathetic tone. Focus on understanding and validation.\n" +
            "Be gentle with any difficult truths. Emphasize hope and growth.",
        ["analytical"] =
            "Use a clear, structured approach. Break down the behavior systematically.\n" +
            "Provide logical explanations and evidence-based insights.\n" +
            "Be objective and balanced.",
        ["brutally_honest"] =
            "Be direct and honest about potential issues. Don't sugarcoat concerns.\n" +
            "Still be respectful and constructive, but prioritize truth over comfort.\n" +
            "Help the user see things they might be avoiding.",
        ["light_funny"] =
            "Use a lighthearted, humorous tone when appropriate. Make the analysis engaging.\n" +
            "Use gentle humor to ease tension while still being helpful.\n" +
            "Keep it fun but meaningful."
    };

    public static string BuildPrompt(BehaviorAnalysisRequest req)
    {
        var sb = new StringBuilder();
        sb.AppendLine(BasePrompt);
        sb.AppendLine();

        var mode = req.AnalysisMode ?? "gentle";
        sb.AppendLine(ModeVariants.TryGetValue(mode, out var variant) ? variant : ModeVariants["gentle"]);

        var hasContext = false;
        if (!string.IsNullOrWhiteSpace(req.RelationshipType) ||
            !string.IsNullOrWhiteSpace(req.RelationshipLength) ||
            !string.IsNullOrWhiteSpace(req.EmotionalState))
        {
            hasContext = true;
        }

        if (hasContext)
        {
            sb.AppendLine();
            sb.AppendLine("Context:");
            if (!string.IsNullOrWhiteSpace(req.RelationshipType))
                sb.AppendLine($"Relationship type: {req.RelationshipType}");
            if (!string.IsNullOrWhiteSpace(req.RelationshipLength))
                sb.AppendLine($"Relationship length: {req.RelationshipLength}");
            if (!string.IsNullOrWhiteSpace(req.EmotionalState))
                sb.AppendLine($"User's emotional state: {req.EmotionalState}");
        }

        sb.AppendLine();
        sb.AppendLine("Behavior to analyze:");
        sb.AppendLine(req.BehaviorDescription);
        sb.AppendLine();
        sb.AppendLine("Please structure your response with:");
        sb.AppendLine("1. Analysis: A clear explanation of what this behavior might mean");
        sb.AppendLine("2. Emotional Insight: What this might indicate about emotions or needs");
        sb.AppendLine("3. Practical Advice: Concrete steps the user can take");
        sb.AppendLine("4. Reassurance: A supportive message to help them feel understood");

        return sb.ToString();
    }

    public static BehaviorAnalysisResponse FormatResponse(string raw, string? modeUsed)
    {
        // Best-effort section extraction; fallback to sensible defaults.
        var analysis = ExtractSection(raw, "Analysis");
        var emotional = ExtractSection(raw, "Emotional Insight");
        var practical = ExtractSection(raw, "Practical Advice");
        var reassurance = ExtractSection(raw, "Reassurance");

        analysis ??= raw.Trim();
        emotional ??= "Consider the emotional context and needs behind this behavior.";
        practical ??= "Take a moment to reflect, then communicate calmly and clearly when you’re ready.";
        reassurance ??= "Your feelings make sense. You deserve clarity and respectful communication.";

        return new BehaviorAnalysisResponse(
            Analysis: analysis.Trim(),
            EmotionalInsight: emotional.Trim(),
            PracticalAdvice: practical.Trim(),
            Reassurance: reassurance.Trim(),
            ModeUsed: (modeUsed ?? "gentle").Trim().ToLowerInvariant()
        );
    }

    private static string? ExtractSection(string raw, string title)
    {
        // Handles headers like "1. Analysis:" or "Analysis:".
        var pattern = $@"(?ims)(?:^|\n)\s*(?:\d+\.\s*)?{Regex.Escape(title)}\s*:?\s*\n(.*?)(?=\n\s*(?:\d+\.\s*)?(?:Analysis|Emotional Insight|Practical Advice|Reassurance)\s*:?\s*\n|\z)";
        var m = Regex.Match(raw, pattern);
        if (!m.Success) return null;
        var v = m.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }
}


