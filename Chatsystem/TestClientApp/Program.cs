using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class TestClient
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: TestClient <username> <password>");
            return;
        }

        string username = args[0];
        string password = args[1];

        try
        {
            TcpClient client = new TcpClient("127.0.0.1", 5000);
            NetworkStream stream = client.GetStream();

            // Try Signup
            SendMessage(stream, $"SIGNUP|{username}|{password}");
            string response = ReadMessage(stream); // Read signup response
            
            if (response != null && response.StartsWith("ERROR"))
            {
                Console.WriteLine($"[{username}] Signup failed: {response}. Trying Login...");
                client.Close();
                
                // Reconnect for Login
                client = new TcpClient("127.0.0.1", 5000);
                stream = client.GetStream();
                SendMessage(stream, $"LOGIN|{username}|{password}");
                response = ReadMessage(stream);
            }

            Console.WriteLine($"[{username}] Auth response: {response}");

            if (response != null && response.StartsWith("OK"))
            {
                // Start listening thread
                Thread listener = new Thread(() => Listen(stream, username));
                listener.Start();

                // Send a test message after a short delay
                Thread.Sleep(1000);
                SendMessage(stream, $"Hello from {username}!");
                
                // Keep alive for a bit to receive messages
                Thread.Sleep(5000);
            }
            
            client.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{username}] Error: {ex.Message}");
        }
    }

    static void Listen(NetworkStream stream, string username)
    {
        try
        {
            while (true)
            {
                string message = ReadMessage(stream);
                if (message == null) break;
                Console.WriteLine($"[{username}] Received: {message}");
            }
        }
        catch
        {
            // Ignore
        }
    }

    static void SendMessage(NetworkStream stream, string message)
    {
        byte[] body = Encoding.UTF8.GetBytes(message);
        int length = body.Length;
        byte[] lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(length));
        stream.Write(lengthBytes, 0, lengthBytes.Length);
        stream.Write(body, 0, body.Length);
    }

    static string ReadMessage(NetworkStream stream)
    {
        byte[] lengthBuffer = new byte[4];
        int read = stream.Read(lengthBuffer, 0, 4);
        if (read == 0) return null;
        int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuffer, 0));
        byte[] buffer = new byte[length];
        read = stream.Read(buffer, 0, length);
        return Encoding.UTF8.GetString(buffer, 0, length);
    }
}
