using System.IO.Pipes;
using System.Text;

namespace DevBar.Core.Ipc;

/// <summary>
/// Named-pipe server hosted by the tray app. Each connection carries one
/// single-line request and receives a text response.
/// </summary>
public sealed class PipeServer : IDisposable
{
    private readonly Func<string, Task<string>> _handler;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public PipeServer(Func<string, Task<string>> handler) => _handler = handler;

    public void Start() => _loop = Task.Run(AcceptLoopAsync);

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                // CurrentUserOnly: only processes running as the same user (and same
                // elevation) may connect, and remote connections are rejected. Without
                // this, any local account could issue KILL-PORT etc. through the pipe.
                var server = new NamedPipeServerStream(
                    IpcProtocol.PipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(_cts.Token);
                _ = Task.Run(() => HandleClientAsync(server));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(500);
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream server)
    {
        await using (server)
        {
            try
            {
                using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                var request = await reader.ReadLineAsync(_cts.Token);
                if (request is null) return;

                var response = await _handler(request.Trim());
                var bytes = Encoding.UTF8.GetBytes(response);
                await server.WriteAsync(bytes, _cts.Token);
                await server.FlushAsync(_cts.Token);
            }
            catch
            {
                // Client disconnected mid-request; nothing to do.
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
