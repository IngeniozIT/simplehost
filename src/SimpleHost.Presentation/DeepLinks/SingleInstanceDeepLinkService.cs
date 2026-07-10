using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleHost.Presentation.DeepLinks;

/// <summary>
/// Keeps SimpleHost to a single running instance and carries <c>simplehost://</c> deep links to that
/// instance. Opening such a link asks Windows to start SimpleHost; without this, every link would
/// launch a fresh copy. On construction the service claims a named mutex: the first process to claim
/// it is the <see cref="IsPrimary">primary</see> and stays running, hosting the app and
/// <see cref="StartListening">listening</see> for links; a later process finds the mutex already held,
/// <see cref="ForwardLink">forwards</see> its link to the primary over a named pipe, and exits — so
/// the one already-running instance handles subsequent links rather than a second copy starting.
/// <para>
/// This is the thin, Windows-only edge of the deep-link feature: the decision of what a link means and
/// whether it needs confirmation lives in the testable view-model layer. This type only owns the
/// single-instance mutex and the pipe that shuttles the raw link text between processes.
/// </para>
/// </summary>
public sealed class SingleInstanceDeepLinkService : IDisposable
{
    private const string MutexName = @"Local\SimpleHost.SingleInstance.Mutex";
    private const string PipeName = "SimpleHost.SingleInstance.DeepLink";
    private const int ConnectTimeoutMilliseconds = 3000;

    private readonly Mutex _mutex;
    private readonly bool _isPrimary;
    private CancellationTokenSource? _listenerCancellation;
    private Task? _listenerTask;
    private bool _disposed;

    /// <summary>
    /// Claims the single-instance mutex, recording whether this process is the first (and therefore
    /// the primary that hosts the running app) or a secondary that should forward its link and exit.
    /// </summary>
    public SingleInstanceDeepLinkService()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out _isPrimary);
    }

    /// <summary>
    /// Whether this process is the primary instance — the first to start, which owns the running app.
    /// A secondary instance (<c>false</c>) should forward any link and exit without starting a copy.
    /// </summary>
    public bool IsPrimary => _isPrimary;

    /// <summary>
    /// Begins listening, on a background thread, for links forwarded by later instances and invokes
    /// <paramref name="onLinkReceived"/> with each raw link as it arrives. Only the primary instance
    /// listens; the callback runs off the UI thread, so the caller must marshal back to it.
    /// </summary>
    /// <param name="onLinkReceived">Invoked with each forwarded raw link.</param>
    /// <exception cref="InvalidOperationException">Thrown when called on a secondary instance.</exception>
    public void StartListening(Action<string> onLinkReceived)
    {
        ArgumentNullException.ThrowIfNull(onLinkReceived);

        if (!_isPrimary)
        {
            throw new InvalidOperationException(
                "Only the primary instance listens for forwarded deep links.");
        }

        _listenerCancellation = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenLoopAsync(onLinkReceived, _listenerCancellation.Token));
    }

    private static async Task ListenLoopAsync(Action<string> onLinkReceived, CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellation).ConfigureAwait(false);

                using var reader = new StreamReader(server);
                var link = await reader.ReadToEndAsync(cancellation).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(link))
                {
                    onLinkReceived(link.Trim());
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                // A forwarding instance disconnected mid-write; drop this link and keep listening.
            }
        }
    }

    /// <summary>
    /// Sends <paramref name="link"/> to the already-running primary instance over the named pipe.
    /// Called by a secondary instance just before it exits, so the primary can act on the link.
    /// </summary>
    /// <param name="link">The raw link text to forward.</param>
    public static void ForwardLink(string link)
    {
        ArgumentNullException.ThrowIfNull(link);

        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
        client.Connect(ConnectTimeoutMilliseconds);

        using var writer = new StreamWriter(client);
        writer.Write(link);
        writer.Flush();
    }

    /// <summary>
    /// Stops the listener and releases the single-instance mutex, so the next launch can become the
    /// new primary.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _listenerCancellation?.Cancel();
        try
        {
            _listenerTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The listener faulted or was cancelled during teardown; nothing left to do.
        }

        _listenerCancellation?.Dispose();

        if (_isPrimary)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}
