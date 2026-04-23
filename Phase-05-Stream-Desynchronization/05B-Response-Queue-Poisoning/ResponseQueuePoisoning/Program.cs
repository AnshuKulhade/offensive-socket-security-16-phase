// VulnerableDesyncServer.cs
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Server
{
    static void Main()
    {
        var listener = new TcpListener(IPAddress.Loopback, 9090);
        listener.Start();
        Console.WriteLine("[Server] Running on 9090...");

        while (true)
        {
            var client = listener.AcceptTcpClient();
            Console.WriteLine("\n[+] New connection");

            Thread t = new Thread(() => Handle(client));
            t.Start();
        }
    }


    static void Handle(TcpClient client)
    {
        var stream = client.GetStream();
        byte[] buf = new byte[1024];

        while (true) // KEEP CONNECTION ALIVE
        {
            try
            {
                // VULNERABLE: assumes full request in one read
                int n = stream.Read(buf, 0, buf.Length);
                if (n == 0) break;

                string req = Encoding.UTF8.GetString(buf, 0, n);

                Console.WriteLine($"\n[Server Received]\n{req}");

                // Split by newline CLRF (parser mismatch)
                string[] commands = req.Split(new[] { "\r\n\r\n", "\n\n" },StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in commands)
                {
                    string cmd = part.Trim();
                    if (string.IsNullOrWhiteSpace(cmd)) continue;

                    Console.WriteLine($"[Parsed CMD] {cmd}");

                    // NORMAL
                    if (cmd.StartsWith("GET /"))
                    {
                        string body = "USER_DATA";
                        string resp =
                            "HTTP/1.1 200 OK\r\n" +
                            $"Content-Length: {body.Length}\r\n" +
                            "\r\n" +
                            body;

                        stream.Write(Encoding.UTF8.GetBytes(resp));
                        Console.WriteLine("[Response] USER_DATA");
                    }

                    // ADMIN (should be protected)
                    else if (cmd.StartsWith("ADMIN") && cmd.Contains("TOKEN=valid"))
                    {
                        string body = "ADMIN_SECRET";
                        string resp =
                            "HTTP/1.1 200 OK\r\n" +
                            $"Content-Length: {body.Length}\r\n" +
                            "\r\n" +
                            body;

                        stream.Write(Encoding.UTF8.GetBytes(resp));
                        Console.WriteLine("[Response] ADMIN_SECRET");
                    }
                }
            }
            catch
            {
                break;
            }
        }

        client.Close();
        Console.WriteLine("[-] Connection closed");
    }
}