namespace GalaxyBatteryCheck.Core;

public sealed class BatteryService : IDisposable
{
    private readonly AdbClient _adb;
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan DumpTimeout = TimeSpan.FromSeconds(20);

    private readonly AdbSession _session = new();

    public BatteryService(string adbPath) => _adb = new AdbClient(adbPath, _session);

    public bool IsStopped => _session.IsStopped;

    public void Dispose() => _session.Dispose();

    public async Task<IReadOnlyList<DeviceEntry>> ListDevicesAsync(CancellationToken cancellationToken = default)
    {
        var output = await _adb.RunAsync(new[] { "devices", "-l" }, ShortTimeout, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return BatteryParser.ParseDevices(output ?? "");
    }

    public async Task<string> SelectDeviceAsync(string? serial, CancellationToken cancellationToken = default)
    {
        var devices = await ListDevicesAsync(cancellationToken).ConfigureAwait(false);
        return BatteryParser.PickDevice(devices, serial);
    }

    private async Task<double?> ReadSysfsAsync(string serial, string name, CancellationToken cancellationToken)
    {
        if (!BatteryParser.IsAllowedSysfsFile(name))
            throw new ArgumentException("허용되지 않은 sysfs 항목", nameof(name));
        var path = $"/sys/class/power_supply/battery/{name}";
        var output = await _adb.RunAsync(new[] { "-s", serial, "shell", "cat", path }, ShortTimeout, optional: true,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return output is null ? null : BatteryParser.ParsePlainNumber(output);
    }

    private Task<string?> GetPropAsync(string serial, string name, CancellationToken cancellationToken) =>
        _adb.RunAsync(new[] { "-s", serial, "shell", "getprop", name }, ShortTimeout, optional: true,
            cancellationToken: cancellationToken);

    public async Task<Report> CollectAsync(string serial, double? ratedMAh, bool skipSysfs,
        CancellationToken cancellationToken = default)
    {
        if (ratedMAh.HasValue && (!double.IsFinite(ratedMAh.Value) || ratedMAh.Value < 100 || ratedMAh.Value > 30000))
            throw new ArgumentException("정격 용량은 100~30000 범위의 유한한 숫자여야 합니다.", nameof(ratedMAh));
        var raw = await _adb.RunAsync(new[] { "-s", serial, "shell", "dumpsys", "battery" }, DumpTimeout,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(raw) || !raw.Contains("level", StringComparison.OrdinalIgnoreCase))
            throw AppText.AdbError("bad_battery");

        var battery = BatteryParser.ParseDump(raw);
        if (battery.LevelPct is null) throw AppText.AdbError("bad_battery");

        if (!skipSysfs)
        {
            if (battery.AsocPct is null)
            {
                var asoc = await ReadSysfsAsync(serial, "fg_asoc", cancellationToken).ConfigureAwait(false);
                if (asoc is > 0 and <= 100)
                {
                    battery.AsocPct = asoc;
                    battery.AsocSource = "sysfs fg_asoc (보조값)";
                }
            }

            if (battery.CycleCount is null)
            {
                foreach (var name in new[] { "cycle_count", "battery_cycle" })
                {
                    var cycles = await ReadSysfsAsync(serial, name, cancellationToken).ConfigureAwait(false);
                    if (cycles is >= 0 and <= 100000)
                    {
                        battery.CycleCount = Math.Truncate(cycles.Value);
                        battery.CycleSource = $"sysfs {name}";
                        break;
                    }
                }
            }
        }

        if (battery.CycleCount is null && battery.UsageRaw is not null)
        {
            battery.CycleCount = BatteryParser.Round(battery.UsageRaw.Value / 100.0, 2);
            battery.CycleSource = BatteryInfo.CycleEstimateSource;
        }

        if (battery.ChargeCounterMAh is > 0 && battery.LevelPct is >= 20 and <= 100)
        {
            battery.ExtrapolatedFullMAh =
                BatteryParser.Round(battery.ChargeCounterMAh.Value / (battery.LevelPct.Value / 100.0), 1);
        }
        // Manual input is optional. Prefer reading the design capacity directly from
        // the device; never infer it from the model name or a guessed battery size.
        double? selectedRatedMAh = ratedMAh;
        if (selectedRatedMAh is not null)
            battery.RatedCapacitySource = "manual";
        else if (!skipSysfs)
        {
            var rawDesignCapacity = await ReadSysfsAsync(serial, "charge_full_design", cancellationToken)
                .ConfigureAwait(false);
            selectedRatedMAh = BatteryParser.DesignCapacityMAh(rawDesignCapacity);
            if (selectedRatedMAh is not null)
                battery.RatedCapacitySource = "sysfs charge_full_design";
        }

        battery.RatedCapacityMAh = selectedRatedMAh;
        if (selectedRatedMAh is > 0 && battery.ExtrapolatedFullMAh is not null)
        {
            battery.ExtrapolatedToRatedPct = BatteryParser.Round(
                battery.ExtrapolatedFullMAh.Value / selectedRatedMAh.Value * 100, 1);
        }
        battery.HealthAssessment = BatteryHealth.Assess(battery);

        var manufacturer = await GetPropAsync(serial, "ro.product.manufacturer", cancellationToken).ConfigureAwait(false);
        var model = await GetPropAsync(serial, "ro.product.model", cancellationToken).ConfigureAwait(false);
        var androidVersion = await GetPropAsync(serial, "ro.build.version.release", cancellationToken).ConfigureAwait(false);

        return new Report
        {
            Application = new ApplicationInfo { DonationUrl = Language.DonationUrl() },
            Device = new DeviceInfo
            {
                Serial = serial,
                Manufacturer = manufacturer,
                Model = model,
                AndroidVersion = androidVersion,
            },
            CheckedAt = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            Battery = battery,
        };
    }
}
