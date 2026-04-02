using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

Console.WriteLine("Запуск WebSocket чату на Fly.io (консольний режим)...");

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:8080");

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

var clients = new ConcurrentDictionary<string, WebSocket>();
var messageHistory = new List<string>();
int clientCounter = 0;

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var ws = await context.WebSockets.AcceptWebSocketAsync();
        clientCounter++;
        var clientId = $"Клієнт #{clientCounter}";

        Console.WriteLine($"✅ {clientId} підключився");

        clients.TryAdd(clientId, ws);

        await SendHistoryAsync(ws);
        await HandleClientAsync(ws, clientId);
    }
    else
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Це WebSocket endpoint. Підключайся через wss://");
    }
});

app.MapGet("/", () => "WebSocket Chat Server is running on Fly.io\nПідключайся: wss://p45.fly.dev/ws");

await app.RunAsync();

// ====================== Допоміжні методи ======================

async Task HandleClientAsync(WebSocket ws, string clientId)
{
    var buffer = new byte[4096];

    try
    {
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count).Trim();

            if (string.IsNullOrWhiteSpace(message)) continue;
            if (message.Equals("off", StringComparison.OrdinalIgnoreCase) || message.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            var formattedMessage = $"{clientId}: {message}";
            Console.WriteLine(formattedMessage);
            messageHistory.Add(formattedMessage);

            await BroadcastMessageAsync(formattedMessage, clientId);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Помилка з {clientId}: {ex.Message}");
    }
    finally
    {
        clients.TryRemove(clientId, out _);
        if (ws.State != WebSocketState.Closed && ws.State != WebSocketState.Aborted)
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Клієнт від'єднався", CancellationToken.None);
        }
        Console.WriteLine($"❌ {clientId} відключився");
    }
}

async Task SendHistoryAsync(WebSocket ws)
{
    if (messageHistory.Count == 0) return;

    var history = string.Join("\n", messageHistory);
    var data = Encoding.UTF8.GetBytes(history);
    await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
}

async Task BroadcastMessageAsync(string message, string excludeClientId)
{
    var data = Encoding.UTF8.GetBytes(message);

    foreach (var (id, clientWs) in clients)
    {
        if (id == excludeClientId || clientWs.State != WebSocketState.Open) continue;

        try
        {
            await clientWs.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch
        {
            clients.TryRemove(id, out _);
        }
    }
}
