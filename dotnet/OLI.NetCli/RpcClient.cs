using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OLI.NetCli;

public class RpcClient : IDisposable
{
    private readonly Process _process;
    private readonly StreamWriter _stdin;
    private readonly StreamReader _stdout;
    private int _nextId = 1;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public RpcClient(string serverPath)
    {
        var psi = new ProcessStartInfo(serverPath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            UseShellExecute = false,
        };
        _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start server");
        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;
    }

    public async Task<JsonNode?> CallAsync(string method, JsonNode? parameters)
    {
        int id = Interlocked.Increment(ref _nextId);
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters ?? new JsonObject()
        };
        string json = request.ToJsonString(_jsonOptions);
        await _stdin.WriteLineAsync(json);
        await _stdin.FlushAsync();

        while (true)
        {
            var line = await _stdout.ReadLineAsync();
            if (line == null) throw new IOException("Server closed");
            if (string.IsNullOrWhiteSpace(line)) continue;
            var doc = JsonNode.Parse(line);
            if (doc?["id"]?.GetValue<int>() == id)
            {
                if (doc?["error"] != null)
                {
                    string msg = doc["error"]?["message"]?.GetValue<string>() ?? "Unknown error";
                    throw new Exception(msg);
                }
                return doc?["result"];
            }
            // ignore notifications
        }
    }

    public void Dispose()
    {
        try { _process.Kill(); } catch { }
        _process.Dispose();
    }
}

