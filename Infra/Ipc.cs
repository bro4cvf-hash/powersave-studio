using System.IO;
using System.IO.Pipes;
using System.Text;

namespace PowerSave.Infra;

public sealed class IpcServer : IDisposable
{
    const string PipeName = "PowerSaveStudio.Ipc.v1";
    readonly CancellationTokenSource _cts = new();

    public event Action<string>? MessageReceived;

    public void Start() => _ = Task.Run(LoopAsync);

    async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);

                string? line;
                using (var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true))
                {
                    line = await reader.ReadLineAsync().ConfigureAwait(false);
                }
                server.Dispose();
                server = null;

                if (!string.IsNullOrWhiteSpace(line))
                {
                    Logger.Info($"ipc: received '{line.Trim()}'");
                    MessageReceived?.Invoke(line.Trim());
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Logger.Warn("ipc: server loop error: " + ex.Message);
                try { await Task.Delay(400, _cts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
            finally
            {
                server?.Dispose();
            }
        }
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        _cts.Dispose();
    }
}

public static class IpcClient
{
    public static bool TrySend(string message)
    {
        for (int attempt = 1; attempt <= 6; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", "PowerSaveStudio.Ipc.v1", PipeDirection.Out);
                // Longer timeout on first attempt when app cold-starting
                int timeout = attempt == 1 ? 1800 : 700;
                client.Connect(timeout);
                using var writer = new StreamWriter(client, Encoding.UTF8, 1024, leaveOpen: false)
                {
                    AutoFlush = true
                };
                writer.WriteLine(message);
                writer.Flush();
                // Give server a moment to read before closing
                client.WaitForPipeDrain();
                Logger.Info($"ipc: sent '{message}' on attempt {attempt}");
                return true;
            }
            catch (TimeoutException ex)
            {
                Logger.Warn($"ipc: send attempt {attempt} timeout: {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"ipc: send attempt {attempt} failed: {ex.Message}");
            }
            // Exponential backoff
            Thread.Sleep(120 * attempt);
        }
        Logger.Warn($"ipc: failed to send '{message}' after retries");
        return false;
    }
}
