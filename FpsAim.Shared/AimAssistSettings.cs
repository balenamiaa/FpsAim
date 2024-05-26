using System.Text.Json;
using System.Text.Json.Serialization;

namespace FpsAim.Shared;


[JsonSerializable(typeof(AimAssistSettings))]
public record AimAssistSettings(float ConfidenceThreshold, float XSensitivity, float YSensitivity, float Dpi, float[][] Breakpoints, float TargetBoxXOffset, float TargetBoxYOffset, float ProgressFactor)
{

    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }

    public static AimAssistSettings? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<AimAssistSettings>(json);
    }
}