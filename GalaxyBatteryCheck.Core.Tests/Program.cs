

using GalaxyBatteryCheck.Core;

if (args.Length == 3 && args[0] == "--hold-adb")
{
    using var heldSession = new AdbSession();
    var heldClient = new AdbClient(args[1], heldSession);
    await heldClient.RunAsync(new[] { "devices", "-l" }, TimeSpan.FromSeconds(20));
    await File.WriteAllTextAsync(args[2], heldSession.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
    await Task.Delay(Timeout.Infinite);
    return 0;
}

var failures = new List<string>();

void Check(string name, bool condition)
{
    if (!condition)
        failures.Add(name);
}

void CheckEqual<T>(string name, T actual, T expected)
{
    if (!Equals(actual, expected))
        failures.Add($"{name}: got {actual}, want {expected}");
}

var fixtureDir = Path.Combine(AppContext.BaseDirectory, "testdata");
var fixturePath = Path.Combine(fixtureDir, "dumpsys_battery.txt");
if (!File.Exists(fixturePath))
{

    var dir = AppContext.BaseDirectory;
    while (dir is not null && !File.Exists(Path.Combine(dir, "testdata", "dumpsys_battery.txt")))
        dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
    if (dir is not null)
        fixturePath = Path.Combine(dir, "testdata", "dumpsys_battery.txt");
}
var fixtureText = File.ReadAllText(fixturePath);

{
    var b = BatteryParser.ParseDump(fixtureText);
    CheckEqual("bsoh", b.BsohPct, 97.0);
    CheckEqual("asoc", b.AsocPct, 96.5);
    CheckEqual("asoc source", b.AsocSource, "Samsung dumpsys");
    CheckEqual("level", b.LevelPct, 85);
    CheckEqual("temperature", b.TemperatureC, 28.5);
    CheckEqual("max temperature", b.MaxTemperatureC, 45.2);
    CheckEqual("first use", b.FirstUseDate, "2024-03-15");
    CheckEqual("charge counter", b.ChargeCounterMAh, 3912.0);
    CheckEqual("usage raw", b.UsageRaw, 41234L);
    Check("native cycle count absent", b.CycleCount is null);
}

{
    var b = BatteryParser.ParseDump("level: 50\ncycle count: 412\n");
    CheckEqual("native cycle count", b.CycleCount, 412.0);
    CheckEqual("native cycle source", b.CycleSource, "Android dumpsys");
}

{
    var b = BatteryParser.ParseDump(
        "mSavedBatteryBsoh: [0]\nmSavedBatteryAsoc: 101\nbattery FirstUseDate: [20241345]\ntemperature: 2000\n");
    Check("bsoh out of range dropped", b.BsohPct is null);
    Check("asoc out of range dropped", b.AsocPct is null);
    Check("invalid date dropped", b.FirstUseDate is null);
    Check("temperature out of range dropped", b.TemperatureC is null);
}

{
    var b = BatteryParser.ParseDump("mSavedBatteryBsoh: [88]\nbattery FirstUseDate: [20240102]\n");
    CheckEqual("bracketed bsoh", b.BsohPct, 88.0);
    CheckEqual("bracketed first use", b.FirstUseDate, "2024-01-02");
}

{
    // No handset data -> never claim a healthy battery.
    var empty = BatteryHealth.Assess(new BatteryInfo());
    CheckEqual("health unavailable", empty.Status, "unknown");
    CheckEqual("health no basis", empty.BasedOn, "none");
    CheckEqual("health 100 good", BatteryHealth.Grade(100), "good");
    CheckEqual("health 90 good", BatteryHealth.Grade(90), "good");
    CheckEqual("health 89.9 caution", BatteryHealth.Grade(89.9), "monitor");
    CheckEqual("health 80 caution", BatteryHealth.Grade(80), "monitor");
    CheckEqual("health 79.9 service", BatteryHealth.Grade(79.9), "service");
    CheckEqual("health zero invalid", BatteryHealth.Grade(0), "unknown");
    CheckEqual("health infinity invalid", BatteryHealth.Grade(double.PositiveInfinity), "unknown");
    CheckEqual("health NaN invalid", BatteryHealth.Grade(double.NaN), "unknown");
    CheckEqual("health >100 invalid", BatteryHealth.Grade(101), "unknown");
    var both = BatteryHealth.Assess(new BatteryInfo { BsohPct = 94, AsocPct = 73 });
    CheckEqual("bsoh takes priority", both.Status, "good");
    CheckEqual("bsoh basis", both.BasedOn, "bsoh");
    Check("disagreement raised", both.IndicatorsDiffer);
    CheckEqual("asoc independently red", both.AsocStatus, "service");
    var fallback = BatteryHealth.Assess(new BatteryInfo { AsocPct = 85 });
    CheckEqual("fallback ASOC", fallback.BasedOn, "asoc");
    CheckEqual("fallback warning", fallback.Status, "monitor");
    Check("no false difference", !fallback.IndicatorsDiffer);
    CheckEqual("design µAh 5000000", BatteryParser.DesignCapacityMAh(5000000), 5000.0);
    CheckEqual("design mAh 4500", BatteryParser.DesignCapacityMAh(4500), 4500.0);
    Check("design unknown zero", BatteryParser.DesignCapacityMAh(0) is null);
    Check("design invalid huge", BatteryParser.DesignCapacityMAh(500000000) is null);
}

CheckEqual("round(2.675, 2)", BatteryParser.Round(2.675, 2), 2.67);
CheckEqual("round(28.45, 1)", BatteryParser.Round(28.45, 1), 28.4);

{
    var output = "* daemon not running; starting now at tcp:5037\n* daemon started successfully\n" +
                 "List of devices attached\nR5CT12345AB device usb:1-1 model:SM_S918N\nZZZ unauthorized usb:1-2\n";
    var devices = BatteryParser.ParseDevices(output);
    CheckEqual("device count", devices.Count, 2);
    if (devices.Count == 2)
    {
        CheckEqual("device[0].serial", devices[0].Serial, "R5CT12345AB");
        CheckEqual("device[0].state", devices[0].State, "device");
        CheckEqual("device[1].serial", devices[1].Serial, "ZZZ");
        CheckEqual("device[1].state", devices[1].State, "unauthorized");
    }
    CheckEqual("empty device list", BatteryParser.ParseDevices("List of devices attached\n\n").Count, 0);
}

{
    var one = new[] { new DeviceEntry("A", "device") };
    var two = new[] { new DeviceEntry("A", "device"), new DeviceEntry("B", "device") };
    var bad = new[] { new DeviceEntry("A", "unauthorized") };

    CheckEqual("single device auto-pick", BatteryParser.PickDevice(one, null), "A");

    try { BatteryParser.PickDevice(two, null); failures.Add("multiple devices should throw"); }
    catch (AdbException ex) { Check("multi-device message mentions --serial", ex.Message.Contains("--serial")); }

    CheckEqual("explicit serial among many", BatteryParser.PickDevice(two, "B"), "B");

    try { BatteryParser.PickDevice(bad, null); failures.Add("unauthorized device should throw"); }
    catch (AdbException ex) { Check("unauthorized message lists state", ex.Message.Contains("A=unauthorized")); }

    try { BatteryParser.PickDevice(bad, "A"); failures.Add("unauthorized explicit serial should throw"); }
    catch (AdbException ex) { Check("unauthorized explicit serial message", ex.Message.Contains("USB 디버깅")); }

    try { BatteryParser.PickDevice(Array.Empty<DeviceEntry>(), "X"); failures.Add("unknown serial should throw"); }
    catch (AdbException) { }

    try { BatteryParser.PickDevice(Array.Empty<DeviceEntry>(), null); failures.Add("no devices should throw"); }
    catch (AdbException) { }
}

{
    CheckEqual("lang ko-KR", Language.FromTag("ko-KR"), "ko");
    CheckEqual("lang ja_JP.UTF-8", Language.FromTag("ja_JP.UTF-8"), "ja");
    CheckEqual("lang en-US", Language.FromTag("en-US"), "en");
    CheckEqual("lang fr-FR falls back to en", Language.FromTag("fr-FR"), "en");
    CheckEqual("lang empty falls back to en", Language.FromTag(""), "en");
    CheckEqual("lang ko.UTF-8", Language.FromTag("ko.UTF-8"), "ko");

    CheckEqual("donation url ko", Language.DonationUrl("ko"), AppInfo.DonationUrl);
    CheckEqual("donation url en", Language.DonationUrl("en"), AppInfo.KofiUrl);
    CheckEqual("donation url ja", Language.DonationUrl("ja"), AppInfo.KofiUrl);
}

{
    var report = new Report
    {
        Application = new ApplicationInfo { DonationUrl = AppInfo.KofiUrl },
        Device = new DeviceInfo { Serial = "R5CT12345AB", Model = "SM-S918N", AndroidVersion = "15", Manufacturer = "samsung" },
        CheckedAt = "2026-01-01T00:00:00+00:00",
        Battery = BatteryParser.ParseDump(fixtureText),
    };
    var json = ReportJson.Serialize(report);
    Check("json keeps Korean literal (ensure_ascii=False equivalent)", json.Contains("꿈을꾸는 파랑새"));
    Check("json field order: bsoh before asoc", json.IndexOf("bsoh_pct", StringComparison.Ordinal) <
                                                 json.IndexOf("asoc_pct", StringComparison.Ordinal));
    Check("json field order: checked_at before battery", json.IndexOf("checked_at", StringComparison.Ordinal) <
                                                           json.IndexOf("\"battery\"", StringComparison.Ordinal));
    Check("json ends with newline", json.EndsWith('\n'));
}

Check("overflow field rejected", BatteryParser.NumberFromField("level: " + new string('9', 400), "level") is null);
Check("overflow sysfs rejected", BatteryParser.ParsePlainNumber(new string('9', 400)) is null);
Check("trailing garbage rejected", BatteryParser.NumberFromField("level: 90junk", "level") is null);
Check("usage long overflow rejected", BatteryParser.ParseDump("mSavedBatteryUsage: 9223372036854775808").UsageRaw is null);
Check("sysfs traversal rejected", !BatteryParser.IsAllowedSysfsFile("../cycle_count"));
Check("sysfs shell characters rejected", !BatteryParser.IsAllowedSysfsFile("cycle_count;id"));
try
{
    await new BatteryService("missing-adb").CollectAsync("test", double.NaN, true);
    failures.Add("NaN rated capacity should throw before ADB starts");
}
catch (ArgumentException) { }
try
{
    await new BatteryService("missing-adb").CollectAsync("test", double.PositiveInfinity, true);
    failures.Add("infinite rated capacity should throw before ADB starts");
}
catch (ArgumentException) { }

foreach (var language in new[] { "ko", "en", "ja" })
{
    Check("translation keys " + language, AppText.Strings["ko"].Keys.ToHashSet().SetEquals(AppText.Strings[language].Keys));
    foreach (var (key, value) in AppText.Strings[language])
    {
        var slots = System.Text.RegularExpressions.Regex.Matches(value, @"\{(\d+)\}");
        var max = slots.Count == 0 ? -1 : slots.Max(m => int.Parse(m.Groups[1].Value));
        var values = Enumerable.Repeat((object)"TEST", max + 1).ToArray();
        try { AppText.Get(language, key, values); }
        catch (FormatException) { failures.Add("invalid translation format: " + language + "/" + key); }
        var expectedSlots = System.Text.RegularExpressions.Regex.Matches(AppText.Strings["ko"][key], @"\{\d+\}").Select(m => m.Value).Order().ToArray();
        Check("translation placeholders " + language + "/" + key, slots.Select(m => m.Value).Order().SequenceEqual(expectedSlots));
        if (language != "ko" && key != "copyright")
            Check("no Korean in translation " + language + "/" + key, !System.Text.RegularExpressions.Regex.IsMatch(value, @"[가-힣]"));
    }
    foreach (var key in new[] { "adb_timeout", "adb_start", "adb_read", "adb_limit", "bad_battery", "multi_device", "no_connected" })
        CheckEqual("localized error " + language + "/" + key, AppText.Error(language, AppText.AdbError(key)), AppText.Get(language, key));
    var stateError = AppText.AdbError("device_state", "SERIAL", "offline");
    CheckEqual("localized state error " + language, AppText.Error(language, stateError), AppText.Get(language, "device_state", "SERIAL", "offline"));
    CheckEqual("localized missing adb " + language, AppText.Error(language, AppText.MissingAdb(new FileNotFoundException())), AppText.Get(language, "adb_missing"));
}
CheckEqual("new version", AppInfo.Version, "1.4.8");
CheckEqual("assembly version", typeof(AppInfo).Assembly.GetName().Version?.ToString(), "1.4.8.0");

Check("trailing decimal point rejected", BatteryParser.ParsePlainNumber("10.") is null);
Check("multiple decimal points rejected", BatteryParser.ParsePlainNumber("1.2.3") is null);
Check("numeric exponent rejected", BatteryParser.ParsePlainNumber("1e99") is null);
Check("missing bracket rejected", BatteryParser.NumberFromField("level: [90", "level") is null);
Check("fractional level rejected", BatteryParser.ParseDump("level: 50.5").LevelPct is null);
Check("implausible charge rejected", BatteryParser.ParseDump("charge counter: 99999999999999999999999999999999999999").ChargeCounterMAh is null);
Check("long usage parsed exactly", BatteryParser.ParseDump("mSavedBatteryUsage: 9223372036854775807").UsageRaw == long.MaxValue);
Check("terminal control characters removed", DisplayText.Clean("a\u001b[31m\r\nb\u202ec") == "a [31m  b c");
var pathological = "level: " + new string(' ', 900000) + "!";
var stopwatch = System.Diagnostics.Stopwatch.StartNew();
Check("long malformed field rejected", BatteryParser.NumberFromField(pathological, "level") is null);
Check("long malformed field completes promptly", stopwatch.Elapsed < TimeSpan.FromSeconds(3));
try { BatteryParser.ParseDevices("not a device header\nSERIAL device"); failures.Add("invalid device header accepted"); }
catch (AdbException) { }
try { BatteryParser.ParseDump(new string('x', 1024 * 1024 + 1)); failures.Add("oversized input accepted"); }
catch (AdbException) { }
var reportDirectory = Path.Combine(Path.GetTempPath(), "GalaxyBatteryCheck-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(reportDirectory);
try
{
    var reportPath = Path.Combine(reportDirectory, "report.json");
    await ReportFile.SaveAsync(reportPath, "old");
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    try { await ReportFile.SaveAsync(reportPath, "new", cancelled.Token); failures.Add("cancelled report save accepted"); }
    catch (OperationCanceledException) { }
    CheckEqual("cancelled save preserves old report", await File.ReadAllTextAsync(reportPath), "old");
    await ReportFile.SaveAsync(reportPath, "new");
    CheckEqual("report replaced successfully", await File.ReadAllTextAsync(reportPath), "new");
    Check("temporary files cleaned", Directory.GetFiles(reportDirectory).Length == 1);
}
finally { Directory.Delete(reportDirectory, true); }

if (args.Length == 2 && args[0] == "--adb")
{
    using var firstSession = new AdbSession();
    using var secondSession = new AdbSession();
    var firstClient = new AdbClient(args[1], firstSession);
    var secondClient = new AdbClient(args[1], secondSession);
    var firstOutput = await firstClient.RunAsync(new[] { "devices", "-l" }, TimeSpan.FromSeconds(20));
    var secondOutput = await secondClient.RunAsync(new[] { "devices", "-l" }, TimeSpan.FromSeconds(20));
    Check("private server returned devices header", firstOutput?.Contains("List of devices attached") == true);
    Check("second private server returned devices header", secondOutput?.Contains("List of devices attached") == true);
    Check("private server ports differ", firstSession.Port != secondSession.Port);
    var stoppedPort = firstSession.Port;
    firstSession.Dispose();
    firstSession.Dispose();
    var stopped = false;
    for (var attempt = 0; attempt < 30; attempt++)
    {
        using var socket = new System.Net.Sockets.TcpClient();
        try { await socket.ConnectAsync(System.Net.IPAddress.Loopback, stoppedPort); }
        catch (System.Net.Sockets.SocketException) { stopped = true; break; }
        await Task.Delay(100);
    }
    Check("disposed server stopped listening", stopped);
    Check("disposed session marked stopped", firstSession.IsStopped);
    var stillRunning = await secondClient.RunAsync(new[] { "devices", "-l" }, TimeSpan.FromSeconds(20));
    Check("other session stays alive", stillRunning?.Contains("List of devices attached") == true);
    secondSession.Dispose();
    if (OperatingSystem.IsWindows())
    {
        var marker = Path.Combine(Path.GetTempPath(), "GalaxyBatteryCheck-" + Guid.NewGuid().ToString("N") + ".txt");
        var info = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(System.Reflection.Assembly.GetExecutingAssembly().Location);
        info.ArgumentList.Add("--hold-adb");
        info.ArgumentList.Add(args[1]);
        info.ArgumentList.Add(marker);
        using var child = System.Diagnostics.Process.Start(info)!;
        try
        {
            for (var attempt = 0; attempt < 200 && !File.Exists(marker) && !child.HasExited; attempt++)
                await Task.Delay(100);
            Check("child private server started", File.Exists(marker));
            if (File.Exists(marker))
            {
                var childPort = int.Parse(await File.ReadAllTextAsync(marker), System.Globalization.CultureInfo.InvariantCulture);
                child.Kill();
                await child.WaitForExitAsync();
                var closed = false;
                for (var attempt = 0; attempt < 30; attempt++)
                {
                    using var socket = new System.Net.Sockets.TcpClient();
                    try { await socket.ConnectAsync(System.Net.IPAddress.Loopback, childPort); }
                    catch (System.Net.Sockets.SocketException) { closed = true; break; }
                    await Task.Delay(100);
                }
                Check("Windows job closes server after forced app exit", closed);
            }
        }
        finally
        {
            if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); }
            File.Delete(marker);
        }
    }
}

if (failures.Count == 0)
{
    Console.WriteLine("ALL TESTS PASSED");
    return 0;
}

Console.WriteLine($"{failures.Count} TEST(S) FAILED:");
foreach (var f in failures)
    Console.WriteLine($"  - {f}");
return 1;
