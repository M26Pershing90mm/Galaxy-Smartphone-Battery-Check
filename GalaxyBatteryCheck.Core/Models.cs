using System.Text.Json.Serialization;

namespace GalaxyBatteryCheck.Core;

public sealed class Report
{
    [JsonPropertyName("application")] public required ApplicationInfo Application { get; set; }
    [JsonPropertyName("device")] public required DeviceInfo Device { get; set; }
    [JsonPropertyName("checked_at")] public required string CheckedAt { get; set; }
    [JsonPropertyName("battery")] public required BatteryInfo Battery { get; set; }
}

public sealed class ApplicationInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = AppInfo.Name;
    [JsonPropertyName("version")] public string Version { get; set; } = AppInfo.Version;
    [JsonPropertyName("author")] public string Author { get; set; } = AppInfo.Author;
    [JsonPropertyName("copyright")] public string Copyright { get; set; } = AppInfo.CopyrightNotice;
    [JsonPropertyName("blog_url")] public string BlogUrl { get; set; } = AppInfo.BlogUrl;
    [JsonPropertyName("donation_url")] public required string DonationUrl { get; set; }
}

public sealed class DeviceInfo
{
    [JsonPropertyName("serial")] public required string Serial { get; set; }
    [JsonPropertyName("manufacturer")] public string? Manufacturer { get; set; }
    [JsonPropertyName("model")] public string? Model { get; set; }
    [JsonPropertyName("android_version")] public string? AndroidVersion { get; set; }
}

public sealed class BatteryInfo
{
    [JsonPropertyName("bsoh_pct")] public double? BsohPct { get; set; }
    [JsonPropertyName("asoc_pct")] public double? AsocPct { get; set; }
    [JsonPropertyName("asoc_source")] public string? AsocSource { get; set; }
    [JsonPropertyName("level_pct")] public int? LevelPct { get; set; }
    [JsonPropertyName("temperature_c")] public double? TemperatureC { get; set; }
    [JsonPropertyName("max_temperature_c")] public double? MaxTemperatureC { get; set; }
    [JsonPropertyName("first_use_date")] public string? FirstUseDate { get; set; }
    [JsonPropertyName("charge_counter_mah")] public double? ChargeCounterMAh { get; set; }
    [JsonPropertyName("usage_raw")] public long? UsageRaw { get; set; }
    [JsonPropertyName("cycle_count")] public double? CycleCount { get; set; }
    [JsonPropertyName("cycle_source")] public string? CycleSource { get; set; }
    [JsonPropertyName("extrapolated_full_mah")] public double? ExtrapolatedFullMAh { get; set; }
    [JsonPropertyName("rated_capacity_mah")] public double? RatedCapacityMAh { get; set; }
    [JsonPropertyName("rated_capacity_source")] public string? RatedCapacitySource { get; set; }
    [JsonPropertyName("health_assessment")] public HealthAssessment? HealthAssessment { get; set; }
    [JsonPropertyName("extrapolated_to_rated_pct")] public double? ExtrapolatedToRatedPct { get; set; }

    public const string CycleEstimateSource = "Samsung 누적 사용량 환산 추정치";
}

public readonly record struct DeviceEntry(string Serial, string State);

public class AdbException : Exception
{
    public AdbException(string message) : base(message) { }
    public AdbException(string message, Exception inner) : base(message, inner) { }
}

public sealed class AdbNotFoundException : AdbException
{
    public AdbNotFoundException(string message, Exception inner) : base(message, inner) { }
}
