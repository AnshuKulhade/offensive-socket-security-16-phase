using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Unicode;
using System.Threading;
namespace BlockingThreadPinning
{
    internal class Program
    {
        static int activeConnections = 0;

        static void Main(string[] args)
        {
            Console.WriteLine("Hello, Socket World!");
            var server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            server.Bind(new IPEndPoint(IPAddress.Loopback, 9002));
            server.Listen(50);
            Console.WriteLine("[*] Server started on port 9002");

            Thread.Sleep(5000);
            ThreadPool.SetMaxThreads(50, 50); // limit worker & IO threads to force starvation quickly
            while (true)
            {

                var client = server.Accept();

                // Explicitly keeping infinite wait (default behavior)
                client.ReceiveTimeout = 0;
                Interlocked.Increment(ref activeConnections);

                //Console.WriteLine("After Accept");
                //var t = new Thread(() =>
                ThreadPool.QueueUserWorkItem(x =>
                {
                    try
                    {
                        ThreadPool.GetAvailableThreads(out int worker, out int io);
                        Console.WriteLine($"[*] Available worker threads: {worker}");
                        Console.WriteLine($"[*] Active connections: {activeConnections}");

                        var buf = new byte[1024];

                        Console.WriteLine("[+] Client connected > thread pinned");

                        //VULNERABILITY LINE
                        int n = client.Receive(buf); // blocks forever NOTE: client.ReceiveTimeout = 0; is here by default 0
                        Console.WriteLine($"[+] Received {n} bytes");

                        //USE FOR NORMAL FLOW NO VULN HERE
                        //var msg = Encoding.UTF8.GetString(buf, 0, n);
                        //Console.WriteLine($"[+] Received {n} bytes" + msg);

                        client.Send(Encoding.UTF8.GetBytes("Hello"));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[ERR] " + ex.Message);
                    }
                    finally
                    {
                        client.Close();
                    }
                });


                /*
                 Using Thread: you need to create and start it manually (t.Start())
                 You can set it as background using t.IsBackground = true
                 Using ThreadPool: tasks are automatically managed, no need to call Start()
                 */

                //t.IsBackground = true;
                //t.Start();
            }
        }
    }

}
