
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

class Program
{
    static void Main()
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 9003));
        listener.Listen(10);

        Console.WriteLine("[SERVER] Listening on 9003... (Telemetry Active)\n");

        while (true)
        {
            var client = listener.Accept();
            Handle(client);
        }
    }

    static void Handle(Socket client)
    {
        byte[] buf = new byte[1024];
        Stopwatch sw = new Stopwatch();

        try
        {
            int bytes = client.Receive(buf);
            string fileName = Encoding.UTF8.GetString(buf, 0, bytes);
            string path = Path.Combine(Path.GetTempPath(), fileName);

            Console.WriteLine($"\n[T1] Checking file: {fileName}\n");

            sw.Start(); // START MASTER CLOCK

            // T1 CHECK 
            if (!File.Exists(path))
            {
                Console.WriteLine("[REJECT] File not found");
                return;
            }
            double t1Time = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"[TELEMETRY] File.Exists completed in {t1Time:F4} ms\n");

            // T1.5 I/O READ (The OS Lock) 
            double readStart = sw.Elapsed.TotalMilliseconds;
            byte[] data = File.ReadAllBytes(path);
            double readEnd = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"[TELEMETRY] File.ReadAllBytes (I/O Lock) held for {readEnd - readStart:F4} ms\n");

            // T1.75 THE GOLDEN WINDOW (CPU Hash) 
            // The file is UNLOCKED here. This is where the attacker strikes.
            double hashStart = sw.Elapsed.TotalMilliseconds;
            using (var sha = SHA256.Create())
            {
                sha.ComputeHash(data);
            }
            double hashEnd = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"[TELEMETRY] SHA256 Hash (Golden Window) open for {hashEnd - hashStart:F4} ms\n");

            // T2 USE 
            double t2Start = sw.Elapsed.TotalMilliseconds;
            string content = File.ReadAllText(path);
            double t2End = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"[TELEMETRY] File.ReadAllText completed in {t2End - t2Start:F4} ms\n");

            sw.Stop();
            Console.WriteLine($"[TELEMETRY] Total Execution Time: {sw.Elapsed.TotalMilliseconds:F4} ms\n");

            if (content.Contains("MALICIOUS"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[PROOF] TOCTOU exploited! Payload executed.\n");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("[EXEC] SAFE DATA executed.\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[ERROR] " + ex.Message);
        }
        finally
        {
            client.Close();
        }
    }
}