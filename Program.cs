using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

class TcpChatServer
{
    private const int port = 9000;
    private TcpListener? listener;
    private ConcurrentDictionary<string, TcpClient> clients = new();
    private StringBuilder messageHistory = new();
    private int clientCounter = 0;

    public async Task StartAsync()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "СЕРВЕРНА СТОРОНА (TCP)";

        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"Сервер запущено на порту {port}.");

        while (true)
        {
            var tcpClient = await listener.AcceptTcpClientAsync();
            _ = Task.Run(() => HandleClientAsync(tcpClient));
        }
    }

    private async Task HandleClientAsync(TcpClient tcpClient)
    {
        var endpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "невідомий";
        clientCounter++;
        var clientId = $"Клієнт #{clientCounter} ({endpoint})";
        clients[endpoint] = tcpClient;

        Console.WriteLine($"\nклієнт підключився: {clientId}");
        await SendHistoryAsync(tcpClient);

        var stream = tcpClient.GetStream();
        var buffer = new byte[4096];

        try
        {
            while (true)
            {
                var bytesRead = await stream.ReadAsync(buffer);
                if (bytesRead == 0) break; // клієнт відключився

                var message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                if (message == "off" || message == "exit" || message == "quit")
                    break;

                var formattedMessage = $"\n{endpoint}: {message}";
                Console.WriteLine(formattedMessage);
                messageHistory.AppendLine(formattedMessage);
                await BroadcastMessageAsync(formattedMessage, endpoint);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nпомилка з клієнтом {endpoint}: {ex.Message}");
        }
        finally
        {
            clients.TryRemove(endpoint, out _);
            tcpClient.Close();
            Console.WriteLine($"\nклієнт від'єднався: {clientId}");
        }
    }

    private async Task SendHistoryAsync(TcpClient tcpClient)
    {
        var history = messageHistory.ToString();
        if (string.IsNullOrEmpty(history)) return;

        var data = Encoding.UTF8.GetBytes(history);
        await tcpClient.GetStream().WriteAsync(data);
    }

    private async Task BroadcastMessageAsync(string message, string excludeEndpoint)
    {
        var data = Encoding.UTF8.GetBytes(message);
        foreach (var (endpoint, client) in clients)
        {
            if (endpoint == excludeEndpoint) continue;
            try
            {
                await client.GetStream().WriteAsync(data);
            }
            catch
            {
                clients.TryRemove(endpoint, out _);
            }
        }
    }

    static async Task Main() => await new TcpChatServer().StartAsync();
}
