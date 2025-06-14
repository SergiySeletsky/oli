using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public record struct EventRecord(string Type, object Payload);

public static class RpcServer
{
    static HttpListener? _listener;
    static readonly List<EventRecord> _events = new();
    static CancellationTokenSource? _cts;

    public static List<EventRecord> DrainEvents()
    {
        lock (_events)
        {
            var copy = new List<EventRecord>(_events);
            _events.Clear();
            return copy;
        }
    }

    public static void Start(int port = 5050)
    {
        if (_listener != null) return;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();
        _cts = new CancellationTokenSource();
        Task.Run(() => ListenLoop(_cts.Token));
    }

    public static bool IsRunning => _listener != null;

    public static void Stop()
    {
        try
        {
            _cts?.Cancel();
            _listener?.Stop();
        }
        catch { }
        finally
        {
            _listener = null;
            _cts = null;
        }
    }

    public static void Notify(object payload, string type = "general")
    {
        lock (_events)
            _events.Add(new EventRecord(type, payload));
    }

    static async Task ListenLoop(CancellationToken token)
    {
        if (_listener == null) return;
        while (!token.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch
            {
                break;
            }
            _ = Task.Run(async () => await HandleContext(ctx), token);
        }
    }

    static async Task HandleContext(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;
        if (req.HttpMethod == "GET" && req.Url!.AbsolutePath == "/events")
        {
            var type = req.QueryString["type"] ?? string.Empty;
            var events = DrainEvents();
            if (!string.IsNullOrEmpty(type)) events = events.FindAll(e => e.Type == type);
            var json = JsonSerializer.Serialize(events);
            var data = Encoding.UTF8.GetBytes(json);
            res.ContentType = "application/json";
            res.ContentLength64 = data.Length;
            await res.OutputStream.WriteAsync(data, 0, data.Length);
        }
        else if (req.HttpMethod == "GET" && req.Url!.AbsolutePath == "/stream")
        {
            var type = req.QueryString["type"] ?? "";
            res.ContentType = "text/event-stream";
            res.Headers.Add("Cache-Control", "no-cache");
            res.SendChunked = true;
            var output = res.OutputStream;
            while (_listener != null && res.OutputStream.CanWrite)
            {
                foreach (var ev in DrainEvents())
                {
                    if (type != string.Empty && ev.Type != type) continue;
                    var json = JsonSerializer.Serialize(ev);
                    var line = Encoding.UTF8.GetBytes($"data: {json}\n\n");
                    await output.WriteAsync(line, 0, line.Length);
                    await output.FlushAsync();
                }
                await Task.Delay(1000);
                if (!res.OutputStream.CanWrite) break;
            }
        }
        else if (req.HttpMethod == "POST" && req.Url!.AbsolutePath == "/notify")
        {
            using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            try
            {
                var obj = JsonSerializer.Deserialize<object>(body);
                if (obj != null) Notify(obj);
            }
            catch { }
            res.StatusCode = 204;
        }
        else
        {
            res.StatusCode = 404;
        }
        res.Close();
    }
}
