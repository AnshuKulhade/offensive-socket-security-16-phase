using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

class ExploitClient
{
    static void Main()
    {
        var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        client.Connect(IPAddress.Loopback, 9000);
        Console.WriteLine("Connected to server");

        while (true)
        {
            Console.Write("Enter message: ");
            string input = Console.ReadLine();

            if (string.IsNullOrEmpty(input)) continue;

            byte[] data = Encoding.UTF8.GetBytes(input);
            client.Send(data);

            byte[] buffer = new byte[1024];
            int n = client.Receive(buffer);

            string response = Encoding.UTF8.GetString(buffer, 0, n);
            Console.WriteLine("Server: " + response);
        }
    }
}
