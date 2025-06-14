using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public static class RpcServer
{
    static HttpListener? _listener;
    static readonly List<object> _events = new();
    static CancellationTokenSource? _cts;

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

    public static void Notify(object payload)
    {
        lock (_events)
            _events.Add(payload);
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
            List<object> copy;
            lock (_events) copy = new List<object>(_events);
            var json = JsonSerializer.Serialize(copy);
            var data = Encoding.UTF8.GetBytes(json);
            res.ContentType = "application/json";
            res.ContentLength64 = data.Length;
            await res.OutputStream.WriteAsync(data, 0, data.Length);
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
