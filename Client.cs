using System.Net.WebSockets;
using System.Text;

class WsChatClient
{
    // WebSocket - це протокол, який дозволяє встановити двостороннє з'єднання між клієнтом і сервером. Він забезпечує низьку затримку та ефективну передачу даних, що робить його ідеальним для чат-додатків
    private readonly string _url = "wss://p45.fly.dev/ws";

    public async Task StartAsync()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "Чат Клієнт";

        using var ws = new ClientWebSocket();

        Console.WriteLine($"Підключаємося до {_url} ...");

        await ws.ConnectAsync(new Uri(_url), CancellationToken.None);
        Console.WriteLine("Підключено успішно! Пишіть повідомлення.");

        _ = Task.Run(() => ReceiveMessagesAsync(ws));

        await SendMessagesAsync(ws);
    }

    private async Task ReceiveMessagesAsync(ClientWebSocket ws)
    {
        var buffer = new byte[8192];
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) break;

                var msg = Encoding.UTF8.GetString(buffer, 0, result.Count).Trim();
                if (!string.IsNullOrEmpty(msg))
                    Console.WriteLine($"\n{msg}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nЗ'єднання втрачено: {ex.Message}");
        }
    }

    private async Task SendMessagesAsync(ClientWebSocket ws)
    {
        while (ws.State == WebSocketState.Open)
        {
            var text = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(text)) continue;

            var data = Encoding.UTF8.GetBytes(text);
            await ws.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);

            if (text.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;
        }
    }

    static async Task Main() => await new WsChatClient().StartAsync();
}