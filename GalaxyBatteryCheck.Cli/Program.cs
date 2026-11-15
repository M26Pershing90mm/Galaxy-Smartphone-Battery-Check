using System.Globalization;
using GalaxyBatteryCheck.Core;

using var lifetime = new CancellationTokenSource();
ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; lifetime.Cancel(); };
Console.CancelKeyPress += cancel;
try
{
    return await Cli.RunAsync(args, lifetime.Token);
}
catch (OperationCanceledException) { return 130; }
catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
{
    var language = Cli.UiLanguage(args);
    var message = ex.Data["message_key"] is string key
        ? AppText.Get(language, key, ex.Data["message_args"] as object[] ?? Array.Empty<object>())
        : AppText.Get(language, ex is ArgumentException ? "invalid_argument" : "adb_start");
    Console.Error.WriteLine(AppText.Get(language, "error_prefix", message));
    return ex is ArgumentException ? 2 : 1;
}
finally { Console.CancelKeyPress -= cancel; }

internal static class Cli
{
    public static string UiLanguage(string[] args)
    {
        var language = Language.WindowsDisplayLanguage();
        for (var i = 0; i + 1 < args.Length; i++)
            if (args[i] == "--language" && AppInfo.SupportedLanguages.Contains(args[i + 1]))
                language = args[i + 1];
        return language;
    }

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var uiLanguage = UiLanguage(args);
        string Text(string key, params object[] values) => AppText.Get(uiLanguage, key, values);
        string? adbOpt = null, serial = null, language = null, save = null;
        double? ratedMAh = null;
        bool version = false, donate = false, blog = false, noSysfs = false, jsonOut = false, redact = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--version": version = true; break;
                case "--donate": donate = true; break;
                case "--blog": blog = true; break;
                case "--no-sysfs": noSysfs = true; break;
                case "--json": jsonOut = true; break;
                case "--redact-serial": redact = true; break;
                case "--language": language = NextArg(args, ref i, uiLanguage, "--language"); break;
                case "--adb": adbOpt = NextArg(args, ref i, uiLanguage, "--adb"); break;
                case "--serial": serial = NextArg(args, ref i, uiLanguage, "--serial"); break;
                case "--save": save = NextArg(args, ref i, uiLanguage, "--save"); break;
                case "--rated-mah":
                    var raw = NextArg(args, ref i, uiLanguage, "--rated-mah");
                    if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed))
                    {
                        Console.Error.WriteLine(Text("rated_cli"));
                        return 2;
                    }
                    ratedMAh = parsed;
                    break;
                case "-h" or "--help":
                    PrintUsage(uiLanguage);
                    return 0;
                default:
                    Console.Error.WriteLine(Text("unknown_option", args[i]));
                    return 2;
            }
        }

        if (version)
        {
            Console.WriteLine($"{Text("heading")} v{AppInfo.Version} [C#] | {AppInfo.AuthorNotice}");
            return 0;
        }

        if (language is not null && !AppInfo.SupportedLanguages.Contains(language))
        {
            Console.Error.WriteLine(Text("bad_language"));
            return 2;
        }

        if (donate || blog)
        {
            var url = donate ? Language.DonationUrl(language) : AppInfo.BlogUrl;
            Console.WriteLine(Text("open_url", url));
            if (!TryOpenUrl(url))
            {
                Console.Error.WriteLine(Text("browser_error", url));
                return 1;
            }
            return 0;
        }

        if (ratedMAh is not null and (< 100 or > 30000))
        {
            Console.Error.WriteLine(Text("rated_cli"));
            return 2;
        }

        var bundledAdb = Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe");
        var adjacentAdb = Path.Combine(AppContext.BaseDirectory, "adb.exe");
        var adbPath = adbOpt ?? (File.Exists(bundledAdb) ? bundledAdb : File.Exists(adjacentAdb) ? adjacentAdb : "adb");
        using var service = new BatteryService(adbPath);

        if (!jsonOut)
        {
            Console.WriteLine($"{Text("heading")} v{AppInfo.Version}");
            Console.WriteLine(AppInfo.AuthorNotice);
            Console.WriteLine();
        }

        try
        {
            var selected = await service.SelectDeviceAsync(serial, cancellationToken);
            var report = await service.CollectAsync(selected, ratedMAh, noSysfs, cancellationToken);
            if (redact)
                report.Device.Serial = RedactSerial(report.Device.Serial);

            report.Application.DonationUrl = Language.DonationUrl(language);
            var encoded = ReportJson.Serialize(report);
            if (jsonOut)
                Console.Write(encoded);
            else
                Display.Print(report, uiLanguage);

            if (save is not null)
            {
                await ReportFile.SaveAsync(save, encoded, cancellationToken);
                Console.Error.WriteLine(Text("saved", save));
            }
            return 0;
        }
        catch (AdbException ex)
        {
            Console.Error.WriteLine(Text("error_prefix", AppText.Error(uiLanguage, ex)));
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine(Text("error_prefix", Text(ex is ArgumentException ? "invalid_argument" : "save_error")));
            return 1;
        }
    }

    private static string NextArg(string[] args, ref int i, string language, string flag)
    {
        if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            var exception = new ArgumentException(AppText.Get(language, "need_value", flag));
            exception.Data["message_key"] = "need_value";
            exception.Data["message_args"] = new object[] { flag };
            throw exception;
        }
        return args[++i];
    }

    private static string RedactSerial(string serial)
    {
        if (serial.Length <= 4)
            return new string('*', serial.Length);
        return new string('*', serial.Length - 4) + serial[^4..];
    }

    private static bool TryOpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void PrintUsage(string language)
    {
        Console.WriteLine(AppText.Get(language, "heading"));
        Console.WriteLine(AppText.Get(language, "usage"));
        Console.WriteLine();
        var options = new (string Flag, string Key)[]
        {
            ("--version", "version"), ("--donate", "donate"), ("--blog", "blog"),
            ("--language ko|en|ja", "language"), ("--adb PATH", "adb"),
            ("--serial SERIAL", "serial"), ("--rated-mah N", "rated-mah"),
            ("--no-sysfs", "no-sysfs"), ("--json", "json"),
            ("--save FILE", "save"), ("--redact-serial", "redact-serial"),
        };
        foreach (var (flag, key) in options)
            Console.WriteLine($"  {flag,-23} {AppText.Get(language, "help_" + key)}");
        Console.WriteLine();
        Console.WriteLine(AppInfo.AuthorNotice);
    }
}

