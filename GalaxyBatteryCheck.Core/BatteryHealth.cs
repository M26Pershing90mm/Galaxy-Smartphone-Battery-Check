using System.Text.Json.Serialization;

namespace GalaxyBatteryCheck.Core;

/// <summary>
/// Indicative bands for device-reported metrics, not an official Samsung service verdict.
/// BSOH is preferred; ASOC is explicitly labelled as a secondary fallback.
/// </summary>
public static class BatteryHealth
{
    public const double GoodThreshold = 90.0;
    public const double ServiceThreshold = 80.0;
    public const double DisagreementThreshold = 10.0;

    public static string Grade(double? percentage)
    {
        if (percentage is not double value || !double.IsFinite(value) || value <= 0 || value > 100)
            return "unknown";
        return value >= GoodThreshold ? "good" : value >= ServiceThreshold ? "monitor" : "service";
    }

    public static HealthAssessment Assess(BatteryInfo battery)
    {
        var bsohGrade = Grade(battery.BsohPct);
        var asocGrade = Grade(battery.AsocPct);
        var basis = bsohGrade != "unknown" ? "bsoh" : asocGrade != "unknown" ? "asoc" : "none";
        var mainGrade = basis == "bsoh" ? bsohGrade : basis == "asoc" ? asocGrade : "unknown";
        var value = basis == "bsoh" ? battery.BsohPct : basis == "asoc" ? battery.AsocPct : null;
        var differs = bsohGrade != "unknown" && asocGrade != "unknown" &&
                      Math.Abs(battery.BsohPct!.Value - battery.AsocPct!.Value) >= DisagreementThreshold;
        return new HealthAssessment
        {
            Status = mainGrade,
            BasedOn = basis,
            ReferencePct = value,
            BsohStatus = bsohGrade,
            AsocStatus = asocGrade,
            IndicatorsDiffer = differs,
        };
    }
}

public sealed class HealthAssessment
{
    [JsonPropertyName("status")] public required string Status { get; init; }
    [JsonPropertyName("based_on")] public required string BasedOn { get; init; }
    [JsonPropertyName("reference_pct")] public double? ReferencePct { get; init; }
    [JsonPropertyName("bsoh_status")] public required string BsohStatus { get; init; }
    [JsonPropertyName("asoc_status")] public required string AsocStatus { get; init; }
    [JsonPropertyName("indicators_differ")] public bool IndicatorsDiffer { get; init; }
}
