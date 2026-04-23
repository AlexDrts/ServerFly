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

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("Сервер запущено на порту 8080");

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var ws = await context.WebSockets.AcceptWebSocketAsync();
        clientCounter++;
        var clientId = $"Клієнт #{clientCounter}";

        Console.WriteLine($"[+] {clientId} підключився");

        clients.TryAdd(clientId, ws);
        await SendHistoryAsync(ws);
        await HandleClientAsync(ws, clientId);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.MapGet("/", () => "Chat Server\nПідключайтесь: wss://<host>/ws");

await app.RunAsync();

async Task HandleClientAsync(WebSocket ws, string clientId)
{
    var buffer = new byte[4096];

    try
    {
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close) break;

            var message = Encoding.UTF8.GetString(buffer, 0, result.Count).Trim();

            if (string.IsNullOrWhiteSpace(message)) continue;
            if (message.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                message.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            var formatted = $"{clientId}: {message}";

            // Вивід повідомлення клієнта на серверній стороні
            Console.WriteLine($"[MSG] {formatted}");

            messageHistory.Add(formatted);
            await BroadcastMessageAsync(formatted, clientId);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERR] Помилка з {clientId}: {ex.Message}");
    }
    finally
    {
        clients.TryRemove(clientId, out _);
        if (ws.State != WebSocketState.Closed && ws.State != WebSocketState.Aborted)
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Закрито", CancellationToken.None);

        Console.WriteLine($"[-] {clientId} відключився");
    }
}

async Task SendHistoryAsync(WebSocket ws)
{
    if (messageHistory.Count == 0) return;

    var history = string.Join("\n", messageHistory);
    var data = Encoding.UTF8.GetBytes(history);
    await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
}

async Task BroadcastMessageAsync(string message, string excludeId)
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
