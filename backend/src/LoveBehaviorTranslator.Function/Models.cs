using System.Text.Json.Serialization;

namespace LoveBehaviorTranslator.Function;

public sealed record BehaviorAnalysisRequest(
    [property: JsonPropertyName("behavior_description")] string BehaviorDescription,
    [property: JsonPropertyName("relationship_type")] string? RelationshipType,
    [property: JsonPropertyName("relationship_length")] string? RelationshipLength,
    [property: JsonPropertyName("emotional_state")] string? EmotionalState,
    [property: JsonPropertyName("analysis_mode")] string? AnalysisMode,
    [property: JsonPropertyName("email_to")] string? EmailTo
)
{
    public BehaviorAnalysisRequest NormalizeAndValidate()
    {
        var desc = (BehaviorDescription ?? "").Trim();
        if (desc.Length < 10) throw new ClientVisibleException(400, "Behavior description must be at least 10 characters.");
        if (desc.Length > 2000) throw new ClientVisibleException(400, "Behavior description must be 2000 characters or less.");

        var mode = (AnalysisMode ?? "gentle").Trim().ToLowerInvariant();
        mode = mode switch
        {
            "gentle" or "analytical" or "brutally_honest" or "light_funny" => mode,
            _ => throw new ClientVisibleException(400, "analysis_mode must be one of: gentle, analytical, brutally_honest, light_funny.")
        };

        var rt = RelationshipType?.Trim().ToLowerInvariant();
        if (rt is not null && rt.Length > 0)
        {
            rt = rt switch
            {
                "dating" or "married" or "situationship" or "friendship" or "other" => rt,
                _ => throw new ClientVisibleException(400, "relationship_type must be one of: dating, married, situationship, friendship, other.")
            };
        }

        var rl = RelationshipLength?.Trim();
        if (rl is not null && rl.Length > 60) throw new ClientVisibleException(400, "relationship_length is too long.");

        var es = EmotionalState?.Trim().ToLowerInvariant();
        if (es is not null && es.Length > 0)
        {
            es = es switch
            {
                "anxious" or "confused" or "hurt" or "hopeful" or "neutral" or "frustrated" => es,
                _ => throw new ClientVisibleException(400, "emotional_state must be one of: anxious, confused, hurt, hopeful, neutral, frustrated.")
            };
        }

        var email = EmailTo?.Trim();
        if (email is not null && email.Length > 254) throw new ClientVisibleException(400, "email_to is invalid.");

        return this with
        {
            BehaviorDescription = desc,
            RelationshipType = string.IsNullOrWhiteSpace(rt) ? null : rt,
            RelationshipLength = string.IsNullOrWhiteSpace(rl) ? null : rl,
            EmotionalState = string.IsNullOrWhiteSpace(es) ? null : es,
            AnalysisMode = mode,
            EmailTo = string.IsNullOrWhiteSpace(email) ? null : email
        };
    }
}

public sealed record BehaviorAnalysisResponse(
    [property: JsonPropertyName("analysis")] string Analysis,
    [property: JsonPropertyName("emotional_insight")] string EmotionalInsight,
    [property: JsonPropertyName("practical_advice")] string PracticalAdvice,
    [property: JsonPropertyName("reassurance")] string Reassurance,
    [property: JsonPropertyName("mode_used")] string ModeUsed
);


