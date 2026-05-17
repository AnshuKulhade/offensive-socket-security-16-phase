using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;
using System.Threading.Tasks;


namespace Phase07_ConnectionReuse
{
    public class Program
    {
        // VULNERABLE: identity bound to connection
        
        static ConcurrentDictionary<Socket, string> identityMap = new();

        static void Main()
        {
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 9006));
            listener.Listen(10);

            Console.WriteLine("Vulnerable Server Started on 9006...");

            while (true)
            {
                var client = listener.Accept();
                Console.WriteLine("[+] Client connected");

                Task.Run(() => HandleClient(client));
            }
        }

        static void HandleClient(Socket client)
        {
            // Default identity
            identityMap[client] = "anonymous";

            byte[] buffer = new byte[1024];
            DateTime adminSessionStart = DateTime.UtcNow;

            try
            {
                // PERSISTENT CONNECTION LOOP (keep-alive style)
                while (true)
                {
                    //FLAW: always trust connection-level identity
                    string currentUser = identityMap[client];


                    int received = client.Receive(buffer);


                    ////GOOD FOR TIME OUT SESSION AUTOMATICALLY LOGGED OUT USER AFTER SPECIFIC TIME 
                    ////if (currentUser == "admin")
                    ////{
                    ////    if ((DateTime.UtcNow - adminSessionStart).TotalSeconds > 60)
                    ////    {
                    ////        Console.WriteLine("[ADMIN TIMEOUT] Closing admin session");
                    ////        break;
                    ////    }
                    ////}

                   


                    //int received = client.Receive(buffer);
                    //if (received == 0)
                    //    break;

                    string msg = Encoding.UTF8.GetString(buffer, 0, received).Trim();

                   

                    Console.WriteLine($"[REQ] {msg} | User={currentUser}");

                    // LOGIN ONCE → persists forever
                    if (msg.StartsWith("LOGIN:"))
                    {
                        string user = msg.Substring(6);
                        identityMap[client] = user;
                        Console.WriteLine($"[LOGIN] {user} authenticated");

                        // DYNAMIC TIMEOUT: Only apply the 10-second timeout if the user is admin
                        if (user == "admin")
                        {
                            client.ReceiveTimeout = 10000; // 10 seconds in milliseconds
                            Console.WriteLine("[!] 10-second idle timeout applied to Admin socket.");
                        }

                        client.Send(Encoding.UTF8.GetBytes($"WELCOME {user}\n"));
                    }
                    // ADMIN ACTION (no re-auth)
                    else if (msg == "ADMIN_ACTION" && currentUser == "admin")
                    {
                        Console.WriteLine($"[ADMIN] Action executed by {currentUser}");

                        client.Send(Encoding.UTF8.GetBytes("ADMIN_ACTION_DONE\n"));
                    }
                    //MORE ADMIN ACTION (no re-auth)
                    else if (msg == "ADMIN_ACTION_DELETE" && currentUser == "admin")
                    {
                        Console.WriteLine($"[ADMIN] Action delete executed by {currentUser}");
                        client.Send(Encoding.UTF8.GetBytes("ADMIN_ACTION_DELETE_DONE\n"));
                    }
                    else
                    {
                        //Auth ≠ Authorization of all actions
                        client.Send(Encoding.UTF8.GetBytes("DENIED\n"));
                    }
                }
            }
            catch (SocketException ex)
            {
                //THIS IS CRITICAL IF ADMIN OR CONNECTED USER DOES NOT PING THE SERVER AFTER LOGIN 
                if (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    Console.WriteLine("[TIMEOUT] Client inactive → closing connection");
                }
                else
                {
                    Console.WriteLine("[SOCKET ERROR] " + ex.Message);
                }
            }
            finally
            {
                Console.WriteLine("[-] Client disconnected");

                // Cleanup → also causes silent logout
                identityMap.TryRemove(client, out _);

                client.Close();
            }
        }
    }
}
