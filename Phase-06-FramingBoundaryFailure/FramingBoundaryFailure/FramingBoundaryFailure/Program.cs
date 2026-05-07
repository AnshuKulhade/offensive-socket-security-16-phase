using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FramingBoundaryFailure
{
    // ============================================================
    // PHASE 06 — FRAMING & BOUNDARY FAILURE
    // Protocol Translation Downgrade (V2 → V1)
    // ============================================================
    //
    // ARCHITECTURE
    //
    // Attacker
    //    ↓
    // V2 Edge Proxy  (Length-Based Framing)
    //    ↓
    // V1 Backend     (Delimiter-Based Framing)
    //
    // ============================================================
    //
    // CORE FAILURE
    //
    // The Edge Proxy trusts:
    //
    //      LEN:<size>|<payload>
    //
    // The Backend trusts:
    //
    //      \n delimited commands
    //
    // The proxy believes it safely normalized ONE frame.
    // The backend reinterprets the same bytes as MULTIPLE commands.
    //
    // ============================================================

    // ============================================================
    // V1 LEGACY BACKEND
    // Delimiter-Based Parser
    // ============================================================
    class V1_Backend
    {
        static void Main()
        {
            new Thread(StartBackend).Start();

            Thread.Sleep(1000);

            StartProxy();
        }

        static void StartBackend()
        {
            var listener =
                new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp);

            listener.Bind(
                new IPEndPoint(
                    IPAddress.Loopback,
                    9006));

            listener.Listen(10);

            Console.WriteLine("[BACKEND - V1] Legacy Backend Online");
            Console.WriteLine("[BACKEND - V1] Parser = Delimiter Based (\\n)");
            Console.WriteLine("");

            while (true)
            {
                var client = listener.Accept();

                new Thread(() => HandleBackend(client)).Start();
            }
        }

        static void HandleBackend(Socket client)
        {
            byte[] buffer = new byte[1024];

            StringBuilder dataBuffer =
                new StringBuilder();

            try
            {
                while (true)
                {
                    int received =
                        client.Receive(buffer);

                    if (received == 0)
                        break;

                    string chunk =
                        Encoding.UTF8.GetString(
                            buffer,
                            0,
                            received);

                    dataBuffer.Append(chunk);

                    while (true)
                    {
                        string current =
                            dataBuffer.ToString();

                        int newline =
                            current.IndexOf('\n');

                        if (newline == -1)
                            break;

                        string message =
                            current
                                .Substring(0, newline)
                                .Trim();

                        dataBuffer.Remove(0, newline + 1);

                        Console.WriteLine($"[BACKEND - FRAME] [{message}]");

                        // ------------------------------------------------
                        // VULNERABLE INTERPRETATION
                        // ------------------------------------------------

                        if (message == "HELLO")
                        {
                            Console.WriteLine(
                                "[BACKEND] Standard greeting accepted");
                        }
                        else if (message == "DELETE_ALL_USERS")
                        {
                            Console.WriteLine(
                                "[BACKEND - CRITICAL] Dangerous command executed");
                        }
                        else if (message.StartsWith("GET:"))
                        {
                            Console.WriteLine(
                                $"[BACKEND] Resource Request => {message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BACKEND ERROR] {ex.Message}");
            }

            client.Close();
        }

        // ============================================================
        // V2 EDGE PROXY
        // Length-Based Framing
        // ============================================================
        static void StartProxy()
        {
            var listener =
                new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp);

            listener.Bind(
                new IPEndPoint(
                    IPAddress.Loopback,
                    9000));

            listener.Listen(10);

            Console.WriteLine("[PROXY - V2] Edge Proxy Online");
            Console.WriteLine("[PROXY - V2] Parser = Length Based");
            Console.WriteLine("");

            while (true)
            {
                var attacker =
                    listener.Accept();

                new Thread(() => HandleProxy(attacker)).Start();
            }
        }

        static void HandleProxy(Socket attacker)
        {
            Socket backend =
                new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp);

            backend.Connect(
                new IPEndPoint(
                    IPAddress.Loopback,
                    9006));

            byte[] buffer = new byte[1024];

            try
            {
                int received =
                    attacker.Receive(buffer);

                if (received == 0)
                    return;

                string raw =
                    Encoding.UTF8.GetString(
                        buffer,
                        0,
                        received);

                Console.WriteLine($"[PROXY - RAW] [{raw}]");

                // ------------------------------------------------
                // V2 PROTOCOL:
                //
                // LEN:<size>|<payload>
                // ------------------------------------------------

                if (!raw.StartsWith("LEN:"))
                {
                    Console.WriteLine(
                        "[PROXY] Invalid frame");

                    return;
                }

                int separator =
                    raw.IndexOf('|');

                string lenString =
                    raw.Substring(
                        4,
                        separator - 4);

                int declaredLength =
                    int.Parse(lenString);

                string payload =
                    raw.Substring(separator + 1);

                Console.WriteLine(
                    $"[PROXY] Declared Length = {declaredLength}");

                Console.WriteLine(
                    $"[PROXY] Payload Accepted");

                // ------------------------------------------------
                // VULNERABILITY
                //
                // Proxy trusts framing completely
                // and forwards payload verbatim
                //
                // It DOES NOT normalize delimiters
                // before downgrading to V1 protocol
                // ------------------------------------------------

                backend.Send(
                    Encoding.UTF8.GetBytes(payload));

                Console.WriteLine(
                    "[PROXY] Payload Forwarded To Backend");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[PROXY ERROR] {ex.Message}");
            }

            attacker.Close();
            backend.Close();
        }
    }

}
