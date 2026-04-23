using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
class Program
{
    static void Main()
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 9004));
        listener.Listen(10);
        Console.WriteLine("[SERVER] Listening on 9004");
        Console.WriteLine("[NOTE] TCP is a stream — Receive() may return partial or merged data");

        while (true)
        {
            var client = listener.Accept();
            Console.WriteLine("[CONNECT] Client connected");
            new Thread(() => Handle(client)).Start();
        }
    }

    static void Handle(Socket client)
    {
        var buf = new byte[1024];

        try
        {
            while (true)
            {
                int n = client.Receive(buf);
                if (n == 0) break;


                // PRINT THE RAW BYTES (The truth)
                //Console.WriteLine("[DEBUG] Hex Bytes:");
                //for (int i = 0; i < n; i++)
                //{
                //    Console.Write($"{buf[i]:X2} "); // Prints bytes in Hexadecimal
                //}
                //Console.WriteLine("\n");



                string raw = Encoding.UTF8.GetString(buf, 0, n);
                Console.WriteLine($"[RECV] Raw buffer: [{raw}]");
                Console.WriteLine($"[RECV] Bytes received: {n}");

                // VULN — treats entire Receive() buffer as one message
                // No framing. No boundary detection.
                // If TCP delivers two logical messages in one buffer,
                // parser sees them merged — trust boundary breaks.
                ParseMessage(raw);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
        }
        finally
        {
            client.Close();
            Console.WriteLine("[DISCONNECT]");
            Console.WriteLine("");
        }
    }

    static void ParseMessage(string rawBuffer)
    {
        // The server attempts to create boundaries using '\n'
        string[] messages = rawBuffer.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // VULNERABILITY: State is scoped to the TCP buffer, not the individual message!
        bool isAuthorized = false;

        foreach (var msg in messages)
        {
            Console.WriteLine($"[PARSE] Evaluating boundary: [{msg}]");

            if (msg.StartsWith("AUTH:GUEST"))
            {
                isAuthorized = true; // Auth succeeds for the guest
                Console.WriteLine("[AUTH] Guest access granted.");
            }
            else if (msg.StartsWith("CMD:EXPORT_USERS"))
            {
                // The smuggled message inherits the authorization state of the previous message!
                if (isAuthorized)
                {
                    Console.WriteLine("[EXEC] Unauthorized user export executed!");
                }
                else
                {
                    Console.WriteLine("[DENIED] Unauthorized command.");
                }
            }
        }
    }
}