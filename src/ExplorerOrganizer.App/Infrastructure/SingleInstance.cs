using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;

namespace ExplorerOrganizer.App.Infrastructure;

/// <summary>
/// One instance per user session. A second launch (e.g. Explorer context menu on 10 selected files) forwards its
/// command line to the running instance through a named pipe and exits.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cts = new();

    public SingleInstance()
    {
        var id = (WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName) + "-" + System.Diagnostics.Process.GetCurrentProcess().SessionId;
        // One instance per data folder: the installed copy, a portable copy and the automated tests (isolated data
        // folder) never forward their command line to each other. string.GetHashCode is randomized: use a stable hash.
        var dataDir = Core.Services.AppPaths.Detect().DataDirectory.TrimEnd('\\').ToLowerInvariant();
        id += "-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(dataDir)))[..12];
        _mutex = new Mutex(initiallyOwned: true, @"Local\ExplorerOrganizer-" + id, out bool created);
        IsFirst = created;
        _pipeName = "ExplorerOrganizer-" + id;
    }

    public bool IsFirst { get; }

    public event Action<string[]>? ArgumentsReceived;

    public bool Forward(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(3000);
            using var w = new StreamWriter(client, Encoding.UTF8);
            foreach (var a in args) w.WriteLine(a);
            w.WriteLine("\u0004");
            w.Flush();
            return true;
        }
        catch (Exception) { return false; }
    }

    public void StartServer()
    {
        Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(_cts.Token);
                    using var r = new StreamReader(server, Encoding.UTF8);
                    var args = new List<string>();
                    string? line;
                    while ((line = await r.ReadLineAsync()) != null && line != "\u0004") args.Add(line);
                    ArgumentsReceived?.Invoke(args.ToArray());
                }
                catch (OperationCanceledException) { break; }
                catch (IOException) { await Task.Delay(200); }
            }
        });
    }

    public void Dispose()
    {
        _cts.Cancel();
        if (IsFirst) { try { _mutex.ReleaseMutex(); } catch { } }
        _mutex.Dispose();
    }
}
