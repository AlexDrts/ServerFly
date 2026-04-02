using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

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

        Console.WriteLine($"✅ {clientId} підключився (WebSocket)");

        clients.TryAdd(clientId, ws);

        // надсилаємо історію повідомлень новому клієнту
        await SendHistoryAsync(ws);

        await HandleClientAsync(clientId, ws);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

// Health check для Fly (обов'язково)
app.MapGet("/", () => "WebSocket Chat Server running on Fly.io\nConnect to /ws");

// головний цикл обробки клієнта
async Task HandleClientAsync(string clientId, WebSocket webSocket)
{
    var buffer = new byte[1024 * 4];

    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count).Trim();

            if (string.IsNullOrWhiteSpace(message)) continue;

            if (message.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                message.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            var formatted = $"{clientId}: {message}";
            Console.WriteLine(formatted);
            messageHistory.Add(formatted);

            await BroadcastAsync(formatted, clientId);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Помилка з {clientId}: {ex.Message}");
    }
    finally
    {
        clients.TryRemove(clientId, out _);
        if (webSocket.State != WebSocketState.Closed && webSocket.State != WebSocketState.Aborted)
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Закрито", CancellationToken.None);
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

async Task BroadcastAsync(string message, string excludeId)
{
    var data = Encoding.UTF8.GetBytes(message);

    foreach (var (id, client) in clients)
    {
        if (id == excludeId || client.State != WebSocketState.Open) continue;

        try
        {
            await client.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch
        {
            clients.TryRemove(id, out _);
        }
    }
}

app.Run();
