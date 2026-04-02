using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

class UdpChatServer
{
    private const int port = 9000;
    private UdpClient? server;
    private ConcurrentDictionary<IPEndPoint, bool> clients = new();
    private StringBuilder messageHistory = new();
    private int clientCounter = 0;

    public async Task StartAsync()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "СЕРВЕРНА СТОРОНА";
        await InitializeServerAsync();

        _ = Task.Run(ReceiveMessagesAsync);
        // await HandleConsoleInputAsync();
        Thread.Sleep(int.MaxValue);
    }

    private async Task InitializeServerAsync()
{
    while (true)
    {
        try
        {
            // !!!!!!!!!!!!!!!!!!!!!!
            server = new UdpClient(new IPEndPoint(IPAddress.Parse("fly-global-services"), port));
            
            Console.WriteLine($"сервер запущено на порту {port} (fly-global-services).");
            break;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"не вдалося запустити сервер: {ex.Message}. очікування...");
            await Task.Delay(1000);
        }
    }
}

    private async Task ReceiveMessagesAsync()
    {
        while (true)
        {
            var result = await server!.ReceiveAsync();
            var message = Encoding.UTF8.GetString(result.Buffer);

            if (!clients.ContainsKey(result.RemoteEndPoint))
            {
                clients[result.RemoteEndPoint] = true;
                clientCounter++;
                await SendHistoryAsync(result.RemoteEndPoint);
                Console.WriteLine($"\nклієнт підключився: {result.RemoteEndPoint} (Клієнт #{clientCounter})");
            }

            if (message == "off" || message == "exit" || message == "quit")
            {
                clients.TryRemove(result.RemoteEndPoint, out _);
                Console.WriteLine($"\nклієнт від'єднався: {result.RemoteEndPoint}");
                continue;
            }

            var formattedMessage = $"\n{result.RemoteEndPoint}: {message}";
            Console.WriteLine(formattedMessage);
            messageHistory.AppendLine(formattedMessage);
            await BroadcastMessageAsync(formattedMessage, result.RemoteEndPoint);
        }
    }

    private async Task SendHistoryAsync(IPEndPoint client)
    {
        var history = Encoding.UTF8.GetBytes(messageHistory.ToString());
        await server!.SendAsync(history, history.Length, client);
    }

    private async Task BroadcastMessageAsync(string message, IPEndPoint? excludeClient = null)
    {
        var data = Encoding.UTF8.GetBytes(message);
        foreach (var client in clients.Keys) // розсилати повідомлення всім клієнтам, крім того, хто його надіслав
        {
            if (!client.Equals(excludeClient))
                await server!.SendAsync(data, data.Length, client);
        }
    }

    private async Task HandleConsoleInputAsync()
    {
        while (true)
        {
            Console.Write("надішліть повідомлення клієнтам: ");
            var input = Console.ReadLine();
            var formattedMessage = $"\nСервер: {input}";
            messageHistory.AppendLine(formattedMessage);
            await BroadcastMessageAsync(formattedMessage);
        }
    }

    static async Task Main() => await new UdpChatServer().StartAsync();
}
