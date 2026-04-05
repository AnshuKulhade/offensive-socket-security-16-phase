using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Program
{
    // Shared account state
    static decimal balance = 1000.00m;

    static void Main()
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 9001));
        listener.Listen(10);
        Console.WriteLine("[SERVER] Listening on 9001");
        Console.WriteLine("[STATE] Starting balance: $" + balance);
        Console.WriteLine("");

        while (true)
        {
            var client = listener.Accept();
            Console.WriteLine("[CONNECT] Client connected");
            Thread.Sleep(1000);
            Handle(client);
            //Handle(client);
        }
    }
    static void Handle(Socket client)
    {
        var buf = new byte[1024];

        try
        {
            int n = client.Receive(buf);
            if (n == 0) return;

            string payload = Encoding.UTF8.GetString(buf, 0, n).Trim();
            Console.WriteLine("[RECV] " + payload);

            // VULN — COMMIT happens immediately after Receive
            // No check that ACK will succeed
            // No two-phase guard
            // No idempotency key
            if (payload.StartsWith("TRANSFER:"))
            {
                string[] parts = payload.Split(':');
                // format: TRANSFER:amount=1000:to=attacker
                decimal amount = decimal.Parse(parts[1].Split('=')[1]);
                string to = parts[2].Split('=')[1];

                // COMMIT — state written before ACK sent
                balance += amount;
                Console.WriteLine("[COMMIT] Transfer executed");
                Console.WriteLine("[COMMIT] Amount : $" + amount);
                Console.WriteLine("[COMMIT] To     : " + to);
                Console.WriteLine("[STATE] New balance: $" + balance);

                // ACK sent after COMMIT
                // If client already RST — this throws
                // But COMMIT already happened — cannot be rolled back
                client.Send(Encoding.UTF8.GetBytes("ACK:transfer_complete"));
                Console.WriteLine("[ACK] Sent successfully");
            }
        }
        catch (SocketException ex)
        {
            // Exception occurs here — during ACK send
            // COMMIT already executed above — silent success
            Console.WriteLine("[ERROR] " + ex.Message);
            Console.WriteLine("[BLIND] Exception caught — but COMMIT already ran");
            Console.WriteLine("[BLIND] Balance is now $" + balance + " — no rollback");
            Console.WriteLine("[BLIND] This error goes to infrastructure team, not security team");
        }
        finally
        {
            client.Close();
            Console.WriteLine("[DISCONNECT]");
            Console.WriteLine("");
        }
    }
}