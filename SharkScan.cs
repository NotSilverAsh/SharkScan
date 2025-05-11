using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

class PortScanner
{
    static async Task Main()
    {
        // ASCII art & description
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
   _________.__               ___.
  /   _____/|  |__  __ ____   \_ |__   ____  ____   
  \_____  \ |  |  \|  |  \  \  || __ \_/ __ \/ __ \  
  /        \|   Y  \  |  /  |_| \_\ \  ___/|  ___/  
 /_______  /|___|  /____/|____/___  /\___  >\___  > 
         \/      \/               \/     \/     \/  
    _________ .__                  __              
   /   _____/ |  |__   ____   ____ |  | __ ___________ 
   \_____  \  |  |  \_/ __ \_/ __ \|  |/ // __ \_  __ \
   /        \ |   Y  \  ___/\  ___/|    <\  ___/|  | \/
  /_______  / |___|  /\___  >\___  >__|_ \\___  >__|   
          \/       \/     \/     \/     \/    \/       

        ⚡ SharkScan v1.0 - Port scanner with bite ⚡
   Scan networks like a predator. Slow but safe, focused, and fishy.
");
        Console.ResetColor();

        // User input
        Console.Write("Enter target IP or hostname: ");
        string target = Console.ReadLine();

        IPAddress ip = null;

        if (IPAddress.TryParse(target, out ip))
        {
            // Already valid IP
        }
        else
        {
            try
            {
                ip = Dns.GetHostEntry(target).AddressList[0];
                Console.WriteLine($"Resolved hostname {target} to IP: {ip}");
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid hostname or IP address.");
                Console.ResetColor();
                return;
            }
        }

        // 🏓 Ping meter
        int pingTime = GetPingTime(ip);
        if (pingTime >= 0)
        {
            Console.WriteLine($"Resolved IP: {ip} ({pingTime} ms)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Couldn't resolve IP: {target} (ping timeout)");
            Console.ResetColor();
            return;
        }

        // Config
        Console.Write("Start port (1 - 65535): ");
        int startPort = int.TryParse(Console.ReadLine(), out int sp) ? sp : 1;

        Console.Write("End port (1 - 65535): ");
        int endPort = int.TryParse(Console.ReadLine(), out int ep) ? ep : 1;

        Console.Write("Timeout (ms): ");
        int timeout = int.TryParse(Console.ReadLine(), out int t) ? t : 1000;

        Console.Write("Scan type (TCP/UDP): ");
        string scanType = Console.ReadLine()?.ToUpper();

        Console.WriteLine($"\nScanning {ip} from port {startPort} to {endPort} via {scanType}. This may take a while...\n");

        int maxConcurrentTasks = 100;
        var tasks = new List<Task>();
        var results = new Dictionary<int, string>();

        for (int port = startPort; port <= endPort; port++)
        {
            if (tasks.Count >= maxConcurrentTasks)
            {
                var completedTask = await Task.WhenAny(tasks);
                tasks.Remove(completedTask);
            }

            if (scanType == "TCP")
                tasks.Add(ScanTcpPort(ip, port, timeout, results));
            else if (scanType == "UDP")
                tasks.Add(ScanUdpPort(ip, port, timeout, results));
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Unknown scan type. Please choose either TCP or UDP.");
                Console.ResetColor();
                break;
            }
        }

        await Task.WhenAll(tasks);
        DisplayScanResults(results);
    }

    // TCP method
    static async Task ScanTcpPort(IPAddress ip, int port, int timeout, Dictionary<int, string> results)
    {
        using var client = new TcpClient();
        try
        {
            var connectTask = client.ConnectAsync(ip, port);
            var result = await Task.WhenAny(connectTask, Task.Delay(timeout));

            if (result == connectTask && client.Connected)
                results[port] = "OPEN";
            else
                results[port] = "CLOSED";
        }
        catch
        {
            results[port] = "CLOSED";
        }
    }

    // UDP method
    static async Task ScanUdpPort(IPAddress ip, int port, int timeout, Dictionary<int, string> results)
    {
        using var udpClient = new UdpClient();
        try
        {
            byte[] buffer = new byte[1];
            await udpClient.SendAsync(buffer, buffer.Length, new IPEndPoint(ip, port));

            var receiveTask = udpClient.ReceiveAsync();
            if (await Task.WhenAny(receiveTask, Task.Delay(timeout)) == receiveTask)
                results[port] = "OPEN";
            else
                results[port] = "FILTERED";
        }
        catch
        {
            results[port] = "FILTERED";
        }
    }

    // Display results
    static void DisplayScanResults(Dictionary<int, string> results)
    {
        Console.WriteLine("\nScan Results:\n");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@" 
   ___________   ___________    
  |   PORT    | |   STATUS   |
  |___________| |____________|");
        Console.ResetColor();

        var sortedResults = results
            .OrderByDescending(r => r.Value == "OPEN")
            .ThenBy(r => r.Key)
            .ToList();

        int closedPortCount = 0;

        foreach (var result in sortedResults)
        {
            string status = result.Value;
            Console.ForegroundColor = status switch
            {
                "OPEN" => ConsoleColor.Green,
                "CLOSED" => ConsoleColor.Red,
                "FILTERED" => ConsoleColor.Yellow,
                _ => ConsoleColor.Gray
            };

            Console.WriteLine($"  | {result.Key,-9} |  {status,-8} |");
            Console.ResetColor();

            if (status == "CLOSED") closedPortCount++;
        }

        if (closedPortCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n   ___________   ___________");
            Console.WriteLine("  |   TOTAL   | |   CLOSED   |");
            Console.WriteLine($"  |   PORTS   | |    {closedPortCount}    |");
            Console.WriteLine("  |___________| |____________|");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine("\nNo closed ports found.");
        }
    }

    // Ping helper
    static int GetPingTime(IPAddress ip)
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send(ip, 1000);
            return reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : -1;
        }
        catch
        {
            return -1;
        }
    }
}
