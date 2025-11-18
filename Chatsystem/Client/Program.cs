using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

class Client
{
    static void Main()
    {
        TcpClient client = new TcpClient("127.0.0.1", 5000);
        NetworkStream stream = client.GetStream();

        // Ask user whether to log in or sign up
        Console.Write("Login or Signup (L/S)? ");
        string? modeInput = Console.ReadLine();
        string mode = (modeInput ?? string.Empty).Trim().ToUpper() == "S" ? "SIGNUP" : "LOGIN";

        Console.Write("Username: ");
        string? username = Console.ReadLine();

        Console.Write("Password: ");
        string? password = Console.ReadLine();

        // Send authentication command: e.g. "LOGIN|user|pass"
        string firstMessage = $"{mode}|{username}|{password}";
        SendMessage(stream, firstMessage);

        // Read server response
        string response = ReadMessage(stream);
        Console.WriteLine($"Server: {response}");

        if (!response.StartsWith("OK|"))
        {
            Console.WriteLine("Authentication failed. Press Enter to exit.");
            Console.ReadLine();
            return;
        }

        Console.WriteLine("You are now logged in. Type messages:");

        while (true)
        {
            string? message = Console.ReadLine();
            SendMessage(stream, message ?? string.Empty);
        }
    }

    static void SendMessage(NetworkStream stream, string message)
    {
        if (message == null)
        {
            message = string.Empty;
        }

        byte[] body = Encoding.UTF8.GetBytes(message);
        int length = body.Length;

        // Send 4-byte length prefix in network byte order (big-endian)
        byte[] lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(length));
        stream.Write(lengthBytes, 0, lengthBytes.Length);

        // Send the actual message bytes
        if (length > 0)
        {
            stream.Write(body, 0, body.Length);
        }
    }

    static string ReadMessage(NetworkStream stream)
    {
        // Read 4-byte length prefix
        byte[] lengthBuffer = new byte[4];
        int read = stream.Read(lengthBuffer, 0, lengthBuffer.Length);
        if (read == 0)
        {
            // Server disconnected
            return string.Empty;
        }

        int messageLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuffer, 0));
        if (messageLength < 0)
        {
            throw new InvalidOperationException("Received negative message length.");
        }

        if (messageLength == 0)
        {
            return string.Empty;
        }

        byte[] buffer = new byte[messageLength];
        int totalRead = 0;
        while (totalRead < messageLength)
        {
            int r = stream.Read(buffer, totalRead, messageLength - totalRead);
            if (r == 0)
            {
                // Server disconnected
                return string.Empty;
            }

            totalRead += r;
        }

        return Encoding.UTF8.GetString(buffer, 0, messageLength);
    }
}
