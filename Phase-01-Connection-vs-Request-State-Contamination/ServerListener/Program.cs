// PURPOSE:
// This server intentionally contains vulnerabilities to demonstrate
// socket-level state contamination and authentication flaws.
// Not intended for production use.

using System.Net.Sockets;
using System.Text;
namespace VulnerableServer
{
    internal class Program
    {
        //MAKING HERE LET THE THREAD SAHRE THE SAME SESSION AND STATE
        //static bool isAuthenticated = false;   // bound to THIS socket lifetime
        //CALLED: Global State Contamination

        static void Main(string[] args)
        {

            //define protocol and ip here for server
            var server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);  //InterNetwork is IPV4, Stream data, TCP Type Protocol 

            //binding the server ip or ipend point here with port number
            //server.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 9000));  //IPAddress.Loopback   // 127.0.0.1 (local only)
            server.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 9000));  //IPAddress.Loopback   // 127.0.0.1 (local only)

            /*System.Net.IPAddress.Any:
             * This line exposes your socket to the network —
                and if you use Any, you just said:

                “Everyone is welcome. Try me.”
             */




            //will accept only 5 connection no more accept the request connection if limit exceed 
            server.Listen(5); //5 = backlog (queue size) backlog: How many incoming connections can WAIT before you accept them
            //NOT number of threads, NOT max clients, NOT total connections
            //Request sending by client accpet by server accept limit 5 


            Console.WriteLine("[*] Listening on 9000");

            //Using loop here for accpet the connection until 
            while (true)
            {
                try
                {
                    Console.WriteLine("Enter Server Aera");
                    //accept the connection
                    var client = server.Accept();
                    Task.Run(() =>
                    {

                        bool isAuthenticated = false;   // bound to THIS socket lifetime


                        while (true)
                        {
                            var buf = new byte[1024];
                            int n = client.Receive(buf);
                            if (n == 0) break;
                            string msg = Encoding.UTF8.GetString(buf, 0, n).Trim();

                            if (msg.StartsWith("AUTH:"))
                            {
                                string pass = msg.Substring(5);
                                isAuthenticated = (pass == "secret");
                                client.Send(Encoding.UTF8.GetBytes(isAuthenticated ? "OK" : "DENIED"));
                                //isAuthenticated = false;

                            }
                            else if (msg == "GET_DATA")
                            {
                                // VULNERABILITY: trusts socket-level auth for every message
                                if (isAuthenticated)
                                    client.Send(Encoding.UTF8.GetBytes("SECRET_DATA: salary=90000"));
                                else
                                    client.Send(Encoding.UTF8.GetBytes("DENIED"));
                            }
                            else if (msg.StartsWith("BECOME:"))
                            {
                                // Simulates mid-connection identity switch — auth NOT reset
                                string newUser = msg.Substring(7);
                                Console.WriteLine($"[!] Identity changed to {newUser} — isAuthenticated still={isAuthenticated}");
                                client.Send(Encoding.UTF8.GetBytes($"You are now {newUser}"));
                            }

                        }
                        client.Close();
                        Console.WriteLine("[*] Client connection closed."); // Connetion closed with client

                    });
                }

                catch (Exception e)
                {

                    Console.WriteLine("Socket Accept Error: " + e.Message);
                }
            }


            // CLIENT EXPLOIT SEQUENCE
            // Step 1: connect and auth as admin
            // Step 2: send BECOME:guest (identity switch)
            // Step 3: send GET_DATA — still gets secret data despite being "guest"

        }
    }
}
