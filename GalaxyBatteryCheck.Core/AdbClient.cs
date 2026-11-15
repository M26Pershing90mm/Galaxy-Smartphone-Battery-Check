using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace GalaxyBatteryCheck.Core;

public sealed class AdbClient
{
    public string AdbPath { get; }
    private const int OutputLimit = 1024 * 1024;

    private readonly AdbSession? _session;

    public AdbClient(string adbPath) : this(adbPath, null) { }

    public AdbClient(string adbPath, AdbSession? session)
    {
        if (string.IsNullOrWhiteSpace(adbPath))
            throw new ArgumentException("ADB 경로가 비어 있습니다.", nameof(adbPath));
        AdbPath = adbPath;
        _session = session;
    }

    private string ResolveExecutable()
    {
        if (Path.IsPathRooted(AdbPath) || AdbPath.Contains('/') || AdbPath.Contains('\\'))
            return Path.GetFullPath(AdbPath);
        var name = OperatingSystem.IsWindows() && !AdbPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? AdbPath + ".exe" : AdbPath;
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var dir = directory.Trim('"');
            if (!Path.IsPathRooted(dir)) continue;
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
        }
        throw AppText.MissingAdb(new FileNotFoundException(name));
    }

    public async Task<string?> RunAsync(IReadOnlyList<string> args, TimeSpan timeout, bool optional = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var executable = ResolveExecutable();
        if (_session is not null) await _session.EnsureStartedAsync(executable, cancellationToken).ConfigureAwait(false);
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
        };
        if (_session is not null)
        {
            startInfo.ArgumentList.Add("-L");
            startInfo.ArgumentList.Add("tcp:127.0.0.1:" + _session.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        foreach (var arg in args) startInfo.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = startInfo };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            process.Start();
            _session?.Track(process);
        }
        catch (Exception ex) when (ex is AdbException or OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode is 2 or 3)
        {
            throw AppText.MissingAdb(ex);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            throw AppText.AdbError("adb_start");
        }

        var stdoutTask = ReadBoundedAsync(process.StandardOutput, cts);
        var stderrTask = ReadBoundedAsync(process.StandardError, cts);
        try
        {
            await Task.WhenAll(process.WaitForExitAsync(cts.Token), stdoutTask, stderrTask).ConfigureAwait(false);
            var stdout = (await stdoutTask.ConfigureAwait(false)).Trim();
            var stderr = (await stderrTask.ConfigureAwait(false)).Trim();
            if (process.ExitCode != 0)
            {
                if (optional) return null;
                throw AppText.AdbError("adb_command", stderr.Length > 0 ? stderr : stdout);
            }
            return stdout;
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            if (optional) return null;
            throw AppText.AdbError("adb_timeout");
        }
        catch (IOException ex)
        {
            if (optional) return null;
            throw AppText.AdbError(ex.Data["output_limit"] is true ? "adb_limit" : "adb_read");
        }
        finally
        {
            cts.Cancel();
            TryKill(process);
            _session?.Untrack(process);
            try { await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false); }
            catch (Exception ex) when (ex is OperationCanceledException or IOException) { }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationTokenSource cancellation)
    {
        var builder = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellation.Token).ConfigureAwait(false)) != 0)
        {
            if (builder.Length + count > OutputLimit)
            {
                var exception = new IOException("Output limit exceeded.");
                exception.Data["output_limit"] = true;
                cancellation.Cancel();
                throw exception;
            }
            builder.Append(buffer, 0, count);
        }
        return builder.ToString();
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException) { }
    }
}
