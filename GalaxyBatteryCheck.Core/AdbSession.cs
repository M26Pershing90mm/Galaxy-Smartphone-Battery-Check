using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace GalaxyBatteryCheck.Core;

public sealed class AdbSession : IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _startup = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly HashSet<Process> _clients = new();
    private Process? _server;
    private WindowsJob? _job;
    private Task _draining = Task.CompletedTask;
    private bool _disposed;
    public int Port { get; private set; }
    public bool IsStopped { get { lock (_gate) return _disposed; } }

    internal async Task EnsureStartedAsync(string executable, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token);
        await _startup.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            Process server;
            lock (_gate)
            {
                linked.Token.ThrowIfCancellationRequested();
                if (_disposed) throw new OperationCanceledException(linked.Token);
                if (_server is not null)
                {
                    if (_server.HasExited) throw AppText.AdbError("adb_session");
                    return;
                }
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                var info = new ProcessStartInfo(executable)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                };
                info.ArgumentList.Add("-L");
                info.ArgumentList.Add("tcp:" + Port.ToString(CultureInfo.InvariantCulture));
                info.ArgumentList.Add("server");
                info.ArgumentList.Add("nodaemon");
                server = new Process { StartInfo = info };
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        _job = new WindowsJob();
                        server.Dispose();
                        server = _job.StartServer(executable, Port);
                    }
                    else
                    {
                        server.Start();
                        _draining = Task.WhenAll(DrainAsync(server.StandardOutput, _stop.Token), DrainAsync(server.StandardError, _stop.Token));
                    }
                    _server = server;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or AdbException)
                {
                    Kill(server);
                    server.Dispose();
                    _job?.Dispose();
                    _job = null;
                    if (ex is Win32Exception win32 && win32.NativeErrorCode is 2 or 3)
                        throw AppText.MissingAdb(ex);
                    throw AppText.AdbError("adb_session");
                }
            }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
            deadline.CancelAfter(TimeSpan.FromSeconds(12));
            try
            {
                while (true)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    if (server.HasExited) throw AppText.AdbError("adb_session");
                    using var socket = new TcpClient();
                    try
                    {
                        await socket.ConnectAsync(IPAddress.Loopback, Port, deadline.Token).ConfigureAwait(false);
                        if (server.HasExited) throw AppText.AdbError("adb_session");
                        return;
                    }
                    catch (SocketException) { }
                    await Task.Delay(100, deadline.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!linked.IsCancellationRequested)
            {
                throw AppText.AdbError("adb_timeout");
            }
        }
        catch (SocketException)
        {
            Dispose();
            throw AppText.AdbError("adb_session");
        }
        catch (ObjectDisposedException) when (_stop.IsCancellationRequested)
        {
            throw new OperationCanceledException(linked.Token);
        }
        catch
        {
            Dispose();
            throw;
        }
        finally { _startup.Release(); }
    }

    internal void Track(Process process)
    {
        lock (_gate)
        {
            if (_disposed) throw new OperationCanceledException(_stop.Token);
            _job?.Attach(process);
            _clients.Add(process);
        }
    }

    internal void Untrack(Process process)
    {
        lock (_gate) _clients.Remove(process);
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken token)
    {
        var buffer = new char[4096];
        try { while (await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false) != 0) { } }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException) { }
    }

    internal static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException) { }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _stop.Cancel();
            foreach (var client in _clients) Kill(client);
            _job?.Dispose();
            if (_server is not null)
            {
                Kill(_server);
                var server = _server;
                _ = _draining.ContinueWith(task => { _ = task.Exception; server.Dispose(); }, TaskScheduler.Default);
            }
        }
    }
}
