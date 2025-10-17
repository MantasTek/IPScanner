using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;

namespace IPScanner;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== IP Scanner ===\n");
        
        // Hämta IP-intervall från användaren
        var (startIP, endIP) = GetIPRange();
        
        if (startIP == null || endIP == null)
        {
            Console.WriteLine("Ogiltiga IP-adresser. Avslutar programmet.");
            return;
        }

        Console.WriteLine($"\nSkannar IP-intervall: {startIP} till {endIP}");
        Console.WriteLine("Detta kan ta en stund...\n");

        // Starta timer för att mäta tiden
        var stopwatch = Stopwatch.StartNew();

        // Skanna IP-adresser
        var results = await ScanIPRangeAsync(startIP, endIP);

        stopwatch.Stop();

        // Visa resultat
        DisplayResults(results, stopwatch.Elapsed);

        Console.WriteLine("\nTryck på valfri tangent för att avsluta...");
        Console.ReadKey();
    }

    /// <summary>
    /// Hämtar IP-intervall från användaren
    /// </summary>
    static (IPAddress startIP, IPAddress endIP) GetIPRange()
    {
        IPAddress? startIP = null;
        IPAddress? endIP = null;

        // Hämta start-IP
        while (startIP == null)
        {
            Console.Write("Ange start-IP (t.ex. 192.168.1.1): ");
            string? input = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(input))
            {
                // Använd standardvärde för demo
                input = "192.168.1.1";
                Console.WriteLine($"Använder standardvärde: {input}");
            }

            if (!IPAddress.TryParse(input, out startIP))
            {
                Console.WriteLine("Ogiltig IP-adress. Försök igen.");
                startIP = null;
            }
        }

        // Hämta slut-IP
        while (endIP == null)
        {
            Console.Write("Ange slut-IP (t.ex. 192.168.1.254): ");
            string? input = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(input))
            {
                // Använd standardvärde för demo
                input = "192.168.1.10";
                Console.WriteLine($"Använder standardvärde: {input}");
            }

            if (!IPAddress.TryParse(input, out endIP))
            {
                Console.WriteLine("Ogiltig IP-adress. Försök igen.");
                endIP = null;
            }
        }

        return (startIP, endIP);
    }

    /// <summary>
    /// Skannar IP-adresser i det angivna intervallet parallellt
    /// </summary>
    static async Task<ConcurrentBag<ScanResult>> ScanIPRangeAsync(IPAddress startIP, IPAddress endIP)
    {
        var results = new ConcurrentBag<ScanResult>();
        var ipList = GenerateIPRange(startIP, endIP);
        
        // Använd ParallelOptions för att begränsa antal samtidiga trådar
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 50 // Begränsa till 50 samtidiga ping-förfrågningar
        };

        // Skanna alla IP-adresser parallellt
        await Task.Run(() =>
        {
            Parallel.ForEach(ipList, parallelOptions, async ip =>
            {
                var result = await PingHostAsync(ip);
                results.Add(result);
                
                // Visa progress
                if (result.IsOnline)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ {ip} är online (svarstid: {result.RoundtripTime}ms)");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write(".");
                }
                Console.ResetColor();
            });
        });

        return results;
    }

    /// <summary>
    /// Skickar ping till en specifik IP-adress
    /// </summary>
    static async Task<ScanResult> PingHostAsync(string ipAddress)
    {
        var result = new ScanResult { IPAddress = ipAddress };
        
        try
        {
            using (var ping = new Ping())
            {
                // Timeout på 1000ms (1 sekund)
                PingReply reply = await ping.SendPingAsync(ipAddress, 1000);
                
                if (reply.Status == IPStatus.Success)
                {
                    result.IsOnline = true;
                    result.RoundtripTime = reply.RoundtripTime;
                    result.Status = "Online";
                }
                else
                {
                    result.IsOnline = false;
                    result.Status = reply.Status.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            result.IsOnline = false;
            result.Status = $"Fel: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Genererar en lista av IP-adresser inom det angivna intervallet
    /// </summary>
    static string[] GenerateIPRange(IPAddress startIP, IPAddress endIP)
    {
        var start = IPToUInt32(startIP);
        var end = IPToUInt32(endIP);
        
        if (end < start)
        {
            // Byt plats om slutet är mindre än start
            var temp = start;
            start = end;
            end = temp;
        }

        var ipList = new string[end - start + 1];
        
        for (uint i = 0; i <= end - start; i++)
        {
            ipList[i] = UInt32ToIP(start + i).ToString();
        }

        return ipList;
    }

    /// <summary>
    /// Konverterar en IP-adress till uint för enklare iteration
    /// </summary>
    static uint IPToUInt32(IPAddress ip)
    {
        byte[] bytes = ip.GetAddressBytes();
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        return BitConverter.ToUInt32(bytes, 0);
    }

    /// <summary>
    /// Konverterar en uint tillbaka till IP-adress
    /// </summary>
    static IPAddress UInt32ToIP(uint ip)
    {
        byte[] bytes = BitConverter.GetBytes(ip);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        return new IPAddress(bytes);
    }

    /// <summary>
    /// Visar resultaten av skanningen
    /// </summary>
    static void DisplayResults(ConcurrentBag<ScanResult> results, TimeSpan elapsedTime)
    {
        Console.WriteLine("\n\n========== SAMMANFATTNING ==========");
        Console.WriteLine($"Skanning slutförd på {elapsedTime.TotalSeconds:F2} sekunder");
        Console.WriteLine($"Totalt antal skannade IP-adresser: {results.Count}");
        
        var onlineHosts = results.Where(r => r.IsOnline).OrderBy(r => r.IPAddress).ToList();
        var offlineHosts = results.Where(r => !r.IsOnline).OrderBy(r => r.IPAddress).ToList();
        
        Console.WriteLine($"Antal online: {onlineHosts.Count}");
        Console.WriteLine($"Antal offline: {offlineHosts.Count}");
        
        if (onlineHosts.Any())
        {
            Console.WriteLine("\n=== ONLINE VÄRDAR ===");
            Console.ForegroundColor = ConsoleColor.Green;
            foreach (var host in onlineHosts)
            {
                Console.WriteLine($"  ✓ {host.IPAddress,-15} - Svarstid: {host.RoundtripTime}ms");
            }
            Console.ResetColor();
        }
        
        // Visa offline värdar om användaren vill
        Console.WriteLine("\nVill du se offline värdar? (j/n)");
        if (Console.ReadKey().Key == ConsoleKey.J)
        {
            Console.WriteLine("\n\n=== OFFLINE VÄRDAR ===");
            Console.ForegroundColor = ConsoleColor.Yellow;
            foreach (var host in offlineHosts)
            {
                Console.WriteLine($"  ✗ {host.IPAddress,-15} - Status: {host.Status}");
            }
            Console.ResetColor();
        }

        // Exportera till fil (valfritt)
        Console.WriteLine("\n\nVill du exportera resultaten till en fil? (j/n)");
        if (Console.ReadKey().Key == ConsoleKey.J)
        {
            ExportResults(results);
        }
    }

    /// <summary>
    /// Exporterar resultat till en textfil
    /// </summary>
    static void ExportResults(ConcurrentBag<ScanResult> results)
    {
        string filename = $"scan_results_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        
        using (var writer = new System.IO.StreamWriter(filename))
        {
            writer.WriteLine($"IP-skanning utförd: {DateTime.Now}");
            writer.WriteLine("=====================================");
            writer.WriteLine();
            
            var sortedResults = results.OrderBy(r => r.IPAddress).ToList();
            
            writer.WriteLine("ONLINE VÄRDAR:");
            foreach (var result in sortedResults.Where(r => r.IsOnline))
            {
                writer.WriteLine($"{result.IPAddress,-15} - Svarstid: {result.RoundtripTime}ms");
            }
            
            writer.WriteLine();
            writer.WriteLine("OFFLINE VÄRDAR:");
            foreach (var result in sortedResults.Where(r => !r.IsOnline))
            {
                writer.WriteLine($"{result.IPAddress,-15} - Status: {result.Status}");
            }
        }
        
        Console.WriteLine($"\n\nResultat exporterat till: {filename}");
    }
}

/// <summary>
/// Klass för att lagra skanningsresultat
/// </summary>
public class ScanResult
{
    public string? IPAddress { get; set; }
    public bool IsOnline { get; set; }
    public long RoundtripTime { get; set; }
    public string? Status { get; set; }
}