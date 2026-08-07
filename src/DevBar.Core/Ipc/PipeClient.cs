using System.IO.Pipes;
using System.Text;

namespace DevBar.Core.Ipc;

public static class PipeClient
{
    /// <summary>Sends one request line; returns the response, or null if no server is running.</summary>
    public static async Task<string?> SendAsync(string request, int connectTimeoutMs = 1000)
    {
        try
        {
            await using var client = new NamedPipeClientStream(
                ".", IpcProtocol.PipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await client.ConnectAsync(connectTimeoutMs);

            var requestBytes = Encoding.UTF8.GetBytes(request + "\n");
            await client.WriteAsync(requestBytes);
            await client.FlushAsync();

            using var reader = new StreamReader(client, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
