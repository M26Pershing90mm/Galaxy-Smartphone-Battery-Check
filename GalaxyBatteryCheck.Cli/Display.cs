using System.Globalization;
using GalaxyBatteryCheck.Core;

internal static class Display
{
    public static void Print(Report report, string language)
    {
        string Text(string key) => AppText.Get(language, key);
        string Str(string? value) => string.IsNullOrEmpty(value) ? Text("none") : DisplayText.Clean(value);
        string Num(double? value, string unit, int places) => value is null ? Text("none") :
            value.Value.ToString("F" + places, CultureInfo.GetCultureInfo(language)) + unit;
        string Suffix(string? source) => string.IsNullOrEmpty(source) ? "" : $" [{AppText.Source(language, source)}]";
        void Row(string key, string value) => Console.WriteLine($"{Text(key)}: {value}");
        var b = report.Battery;
        Console.WriteLine(new string('=', 52));
        Console.WriteLine(Text("heading"));
        Row("model", Str(report.Device.Model));
        Row("android_version", Str(report.Device.AndroidVersion));
        Row("checked_at", report.CheckedAt);
        Row("bsoh", Num(b.BsohPct, "%", 2));
        Row("asoc", Num(b.AsocPct, "%", 2) + Suffix(b.AsocSource));
        var assessment = b.HealthAssessment ?? BatteryHealth.Assess(b);
        Row("health", Text("health_" + assessment.Status));
        Console.WriteLine(Text("health_basis_" + assessment.BasedOn));
        if (assessment.IndicatorsDiffer) Console.WriteLine(Text("health_disagree"));
        Row("level_pct", b.LevelPct is int level ? $"{level}%" : Text("none"));
        Row("temperature_c", Num(b.TemperatureC, " °C", 1));
        Row("max_temperature_c", Num(b.MaxTemperatureC, " °C", 1));
        Row("first_use_date", Str(b.FirstUseDate));
        Row("cycle_count", Num(b.CycleCount, " " + Text("cycles"), b.CycleSource == BatteryInfo.CycleEstimateSource ? 2 : 0) + Suffix(b.CycleSource));
        Row("usage_raw", b.UsageRaw?.ToString(CultureInfo.InvariantCulture) ?? Text("none"));
        Row("charge_counter_mah", Num(b.ChargeCounterMAh, " mAh", 2));
        Row("extrapolated_full_mah", Num(b.ExtrapolatedFullMAh, " mAh", 1));
        Row("rated_capacity_mah", Num(b.RatedCapacityMAh, " mAh", 1));
        Row("rated_capacity_source", b.RatedCapacitySource is null ? Text("none") :
            Text(b.RatedCapacitySource == "manual" ? "rated_manual" : "rated_device"));
        if (b.RatedCapacityMAh is not null) Row("extrapolated_to_rated_pct", Num(b.ExtrapolatedToRatedPct, "%", 1));
        Console.WriteLine(new string('-', 52));
        Console.WriteLine(Text("independent"));
        Console.WriteLine(Text("notice"));
        Console.WriteLine(Text("cli_note"));
        Row("blog_label", AppInfo.BlogUrl);
        Row("donation_label", report.Application.DonationUrl);
    }
}
