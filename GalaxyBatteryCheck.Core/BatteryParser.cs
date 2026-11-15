using System.Globalization;

namespace GalaxyBatteryCheck.Core;

public static class BatteryParser
{
    private const int InputLimit = 1024 * 1024;

    private static void ValidateInput(string text)
    {
        if (text.Length > InputLimit) throw AppText.AdbError("adb_limit");
    }

    private static string? FieldValue(string text, string field)
    {
        ValidateInput(text);
        foreach (var line in text.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon < 0 || !line[..colon].Trim().Equals(field, StringComparison.OrdinalIgnoreCase)) continue;
            var value = line[(colon + 1)..].Trim();
            if (value.Length > 64) return null;
            if (value.StartsWith('['))
            {
                if (!value.EndsWith(']')) return null;
                value = value[1..^1].Trim();
            }
            return value;
        }
        return null;
    }

    public static double? NumberFromField(string text, string field)
    {
        var value = FieldValue(text, field);
        return value is null ? null : ParsePlainNumber(value);
    }

    private static double? ParseFinite(string text) =>
        double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
        && double.IsFinite(value) ? value : null;

    public static double? Percentage(string text, string field)
    {
        var value = NumberFromField(text, field);
        return value is > 0 and <= 100 ? value : null;
    }

    public static string? ParseFirstUseDate(string text)
    {
        var value = FieldValue(text, "battery FirstUseDate") ?? FieldValue(text, "mSavedBatteryFirstUseDate");
        if (value is null || value.Length != 8 || value.Any(c => c < '0' || c > '9')) return null;
        return DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    public static double Round(double value, int digits) =>
        double.Parse(value.ToString("F" + digits, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    public static BatteryInfo ParseDump(string text)
    {
        var battery = new BatteryInfo { AsocSource = null };

        var bsoh = Percentage(text, "mSavedBatteryBsoh");
        if (bsoh is not null) battery.BsohPct = bsoh;

        var asoc = Percentage(text, "mSavedBatteryAsoc");
        if (asoc is not null)
        {
            battery.AsocPct = asoc;
            battery.AsocSource = "Samsung dumpsys";
        }

        var level = NumberFromField(text, "level");
        if (level is >= 0 and <= 100 && level.Value == Math.Truncate(level.Value)) battery.LevelPct = (int)level.Value;

        var temperature = NumberFromField(text, "temperature");
        if (temperature is >= -400 and <= 1000) battery.TemperatureC = Round(temperature.Value / 10, 1);

        var maxTemp = NumberFromField(text, "mSavedBatteryMaxTemp");
        if (maxTemp is >= 0 and <= 1000) battery.MaxTemperatureC = Round(maxTemp.Value / 10, 1);

        battery.FirstUseDate = ParseFirstUseDate(text);

        var chargeCounter = NumberFromField(text, "charge counter");
        if (chargeCounter is >= 0 and <= 1000000000) battery.ChargeCounterMAh = Round(chargeCounter.Value / 1000, 2);

        var usageRaw = FieldValue(text, "mSavedBatteryUsage");
        if (long.TryParse(usageRaw, NumberStyles.None, CultureInfo.InvariantCulture, out var usage) && usage >= 0)
            battery.UsageRaw = usage;

        foreach (var field in new[] { "cycle count", "cycle_count", "cycleCount" })
        {
            var native = NumberFromField(text, field);
            if (native is >= 0 and <= 1000000)
            {
                battery.CycleCount = Math.Truncate(native.Value);
                battery.CycleSource = "Android dumpsys";
                break;
            }
        }

        return battery;
    }

    private static readonly string[] AllowedSysfsFiles =
        { "cycle_count", "battery_cycle", "fg_asoc", "charge_full", "charge_full_design" };

    public static bool IsAllowedSysfsFile(string name) => AllowedSysfsFiles.Contains(name);

    public static double? ParsePlainNumber(string text)
    {
        text = text.Trim();
        if (text.Length is 0 or > 64) return null;
        var index = text.StartsWith('-') ? 1 : 0;
        var digits = 0;
        var dot = false;
        for (; index < text.Length; index++)
        {
            var c = text[index];
            if (c >= '0' && c <= '9') { digits++; continue; }
            if (c == '.' && !dot && digits > 0 && index + 1 < text.Length) { dot = true; continue; }
            return null;
        }
        return digits > 0 ? ParseFinite(text) : null;
    }

    /// <summary>
    /// Common power_supply charge_full_design representations are either mAh or µAh.
    /// Values outside these unambiguous ranges must not be guessed or auto-filled.
    /// </summary>
    public static double? DesignCapacityMAh(double? raw)
    {
        if (raw is not double value || !double.IsFinite(value) || value <= 0)
            return null;
        var mah = value > 30000 && value <= 30000000 ? value / 1000.0 : value;
        return mah is >= 100 and <= 30000 ? Round(mah, 1) : null;
    }

    public static IReadOnlyList<DeviceEntry> ParseDevices(string output)
    {
        ValidateInput(output);
        var lines = output.Replace("\r\n", "\n").Split('\n');
        var start = Array.FindIndex(lines, line => line.Trim().Equals("List of devices attached", StringComparison.Ordinal));
        if (start < 0) throw AppText.AdbError("bad_devices");
        var devices = new List<DeviceEntry>();
        for (var i = start + 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('*')) continue;
            if (line.Length > 4096 || line.Any(c => char.IsControl(c) && c != '\t'))
                throw AppText.AdbError("bad_devices");
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) devices.Add(new DeviceEntry(parts[0], parts[1]));
        }
        return devices;
    }

    public static string PickDevice(IReadOnlyList<DeviceEntry> devices, string? serial)
    {
        if (!string.IsNullOrEmpty(serial))
        {
            var match = devices.FirstOrDefault(d => d.Serial == serial);
            if (match.Serial is null)
                throw AppText.AdbError("unknown_device", serial);
            if (match.State != "device")
                throw AppText.AdbError("device_state", serial, match.State);
            return serial;
        }

        var connected = devices.Where(d => d.State == "device").Select(d => d.Serial).ToList();
        if (connected.Count == 1)
            return connected[0];
        if (connected.Count > 1)
            throw AppText.AdbError("multi_device");
        if (devices.Count > 0)
        {
            var states = string.Join(", ", devices.Select(d => $"{d.Serial}={d.State}"));
            throw AppText.AdbError("no_usable", states);
        }
        throw AppText.AdbError("no_connected");
    }
}
