using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Read WebSocket origin filter settings from configuration
var wsConfig = builder.Configuration.GetSection("WebSocket");
var allowedOrigins = wsConfig.GetSection("AllowedOrigins").Get<string[]>() ?? new string[0];
var allowAllOrigins = wsConfig.GetValue<bool>("AllowAllOrigins");

app.UseWebSockets(); // IIS WebSocket feature must be enabled

// Simple in-memory session map: sessionId -> WebSocket
var sockets = new ConcurrentDictionary<string, WebSocket>();

// Health probe
app.MapGet("/api/health", () => Results.Json(new { ok = true }));

// Debug endpoints
app.MapGet("/api/chatbridge/pid", () => Results.Json(new { pid = Environment.ProcessId }));
app.MapGet("/api/chatbridge/sessions", () => Results.Json(new { count = sockets.Count, sessions = sockets.Keys.ToArray() }));
app.MapGet("/api/chatbridge/session/{id}", (string id) => Results.Json(new { exists = sockets.ContainsKey(id) }));

// WS endpoint: /ws?sessionId=...
app.Map("/ws", async ctx =>
{
    if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }

    // Basic Origin allow-list (since WS doesn't use CORS):
    if (!allowAllOrigins)
    {
        var origin = ctx.Request.Headers.Origin.ToString();
        if (!allowedOrigins.Contains(origin)) { ctx.Response.StatusCode = 403; return; }
    }

    var sid = ctx.Request.Query["sessionId"].ToString();
    if (string.IsNullOrWhiteSpace(sid)) { ctx.Response.StatusCode = 400; return; }

    using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
    sockets[sid] = ws;

    var buffer = new byte[64 * 1024];
    try
    {
        // Optional: keep the socket alive; we don't consume unsolicited messages here.
        while (ws.State == WebSocketState.Open)
        {
            var r = await ws.ReceiveAsync(buffer, CancellationToken.None);
            if (r.MessageType == WebSocketMessageType.Close) break;
        }
    }
    finally
    {
        sockets.TryRemove(sid, out _);
        try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { }
    }
});

// HTTP invoke endpoint: POST /api/chatbridge/invoke
app.MapPost("/api/chatbridge/invoke", async (HttpContext ctx) =>
{
    var req = await ctx.Request.ReadFromJsonAsync<InvokeReq>() ?? new(null, null, null);
    if (string.IsNullOrWhiteSpace(req.sessionId))
        return Results.Json(new { ok = false, error = "missing sessionId" }, statusCode: 400);

    if (!sockets.TryGetValue(req.sessionId!, out var ws) || ws.State != WebSocketState.Open)
        return Results.Json(new { ok = false, error = "No active client" }, statusCode: 404);

    var id = Guid.NewGuid().ToString("n");
    var payload = JsonSerializer.Serialize(new { id, tool = req.tool, args = req.args });
    await ws.SendAsync(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text, true, CancellationToken.None);

    // Wait for matching reply { id, ok, result|error } with a timeout
    var buffer = new byte[128 * 1024];
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    try
    {
        while (!cts.Token.IsCancellationRequested)
        {
            var r = await ws.ReceiveAsync(buffer, cts.Token);
            var json = Encoding.UTF8.GetString(buffer, 0, r.Count);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("id", out var m) && m.GetString() == id)
            {
                return Results.Text(json, "application/json");
            }
        }
        return Results.Json(new { ok = false, error = "timeout" }, statusCode: 504);
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, error = ex.Message }, statusCode: 500);
    }
});

app.Run();

record InvokeReq(string? sessionId, string? tool, object?[]? args);
