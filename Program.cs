using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// UDP-чат на localhost. Имена заданы в коде: Alex и Queny. (Лаб. 7 — Git)
/// Запуск двух окон:
///   UdpMiniChat.exe alex
///   UdpMiniChat.exe queny
/// </summary>
internal static class Program
{
    public const string UserAlex = "Alex";
    public const string UserQueny = "Queny";

    private const int PortAlexListen = 7001;
    private const int PortQuenyListen = 7002;

    private static readonly IPAddress TargetHost = IPAddress.Loopback;
    private static string _userName = "";
    private static int _listenPort;
    private static int _sendPort;
    private static readonly CancellationTokenSource AppCts = new();

    private static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        if (args.Length < 1)
        {
            Console.WriteLine("Укажите режим: alex или queny");
            Console.WriteLine($"  Пример: UdpMiniChat.exe alex   (слушает {PortAlexListen}, шлёт на {PortQuenyListen}, имя «{UserAlex}»)");
            Console.WriteLine($"           UdpMiniChat.exe queny (слушает {PortQuenyListen}, шлёт на {PortAlexListen}, имя «{UserQueny}»)");
            return;
        }

        var mode = args[0].Trim();
        if (mode.Equals("alex", StringComparison.OrdinalIgnoreCase))
        {
            _userName = UserAlex;
            _listenPort = PortAlexListen;
            _sendPort = PortQuenyListen;
        }
        else if (mode.Equals("queny", StringComparison.OrdinalIgnoreCase))
        {
            _userName = UserQueny;
            _listenPort = PortQuenyListen;
            _sendPort = PortAlexListen;
        }
        else
        {
            Console.WriteLine("Неверный режим. Используйте: alex или queny");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Слушаю UDP :{_listenPort}, шлю на {TargetHost}:{_sendPort}, вы — «{_userName}».");
        Console.WriteLine("Пустая строка — выход. Входящие сообщения с меткой времени.");
        Console.WriteLine();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(AppCts.Token);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            AppCts.Cancel();
        };

        var receiveTask = ReceiveLoopAsync(linked.Token);
        await SendLoopAsync(linked.Token);
        AppCts.Cancel();
        try { await receiveTask; } catch (OperationCanceledException) { /* ok */ }
        Console.WriteLine("Выход.");
    }

    private static async Task SendLoopAsync(CancellationToken ct)
    {
        using UdpClient sender = new();
        var remote = new IPEndPoint(TargetHost, _sendPort);

        while (!ct.IsCancellationRequested)
        {
            Console.Write("> ");
            string? line = Console.ReadLine();
            if (line is null) break;
            if (string.IsNullOrWhiteSpace(line))
            {
                AppCts.Cancel();
                break;
            }

            string payload = $"{_userName}: {line}";
            byte[] data = Encoding.UTF8.GetBytes(payload);
            await sender.SendAsync(data, remote, ct);
        }
    }

    private static async Task ReceiveLoopAsync(CancellationToken ct)
    {
        using UdpClient receiver = new(_listenPort);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await receiver.ReceiveAsync(ct);
                string text = Encoding.UTF8.GetString(result.Buffer);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {text}");
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted) { }
    }
}
