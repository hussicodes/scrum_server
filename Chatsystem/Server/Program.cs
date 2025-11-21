using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

class Program
{
    private static Database db = Database.Instance;
    private static List<TcpClient> _connectedClients = new List<TcpClient>();
    private static readonly object _lock = new object();

    static void Main()
    {
        TcpListener server = new TcpListener(IPAddress.Any, 5000);
        server.Start();
        Console.WriteLine("Server started... Waiting for clients...");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            Console.WriteLine("A client has connected.");

            Thread clientThread = new Thread(HandleClient);
            clientThread.Start(client);
        }
    }

    static void HandleClient(object obj)
    {
        TcpClient client = (TcpClient)obj;
        NetworkStream stream = client.GetStream();

        // First message is an authentication command (SIGNUP|username|password or LOGIN|username|password)
        string firstMessage = ReadMessage(stream);
        if (firstMessage == null)
        {
            client.Close();
            return;
        }

        string[] parts = firstMessage.Split('|');
        if (parts.Length != 3)
        {
            SendMessage(stream, "ERROR|Invalid command format.");
            client.Close();
            return;
        }

        string command = parts[0];
        string username = parts[1];
        string password = parts[2];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            SendMessage(stream, "ERROR|Username and password are required.");
            client.Close();
            return;
        }

        if (command == "SIGNUP")
        {
            if (db.UserExists(username))
            {
                SendMessage(stream, "ERROR|User already exists.");
                client.Close();
                return;
            }

            db.AddUser(username, password);
            SendMessage(stream, "OK|Signup successful.");
        }
        else if (command == "LOGIN")
        {
            if (!db.VerifyPassword(username, password))
            {
                SendMessage(stream, "ERROR|Invalid username or password.");
                client.Close();
                return;
            }

            SendMessage(stream, "OK|Login successful.");
        }
        else
        {
            SendMessage(stream, "ERROR|Unknown command.");
            client.Close();
            return;
        }

        Console.WriteLine($"{username} is authenticated and joined the chat!");

        lock (_lock)
        {
            _connectedClients.Add(client);
        }

        while (true)
        {
            try
            {
                string message = ReadMessage(stream);
                if (message == null)
                {
                    Console.WriteLine($"{username} disconnected.");
                    break;
                }

                Console.WriteLine($"[{username}]: {message}");
                BroadcastMessage($"[{username}]: {message}");
            }
            catch
            {
                Console.WriteLine($"{username} disconnected unexpectedly.");
                break;
            }
        }

        lock (_lock)
        {
            _connectedClients.Remove(client);
        }
        client.Close();
    }

    static void BroadcastMessage(string message)
    {
        lock (_lock)
        {
            foreach (var client in _connectedClients)
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    SendMessage(stream, message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error broadcasting to client: {ex.Message}");
                }
            }
        }
    }

    // Send a length-prefixed message to the client.
    static void SendMessage(NetworkStream stream, string message)
    {
        if (message == null)
        {
            message = string.Empty;
        }

        byte[] body = Encoding.UTF8.GetBytes(message);
        int length = body.Length;

        byte[] lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(length));
        stream.Write(lengthBytes, 0, lengthBytes.Length);

        if (length > 0)
        {
            stream.Write(body, 0, body.Length);
        }
    }

    // Read a length-prefixed message from the stream.
    // Returns null if the client disconnected cleanly.
    static string ReadMessage(NetworkStream stream)
    {
        // Read 4-byte length prefix
        byte[] lengthBuffer = new byte[4];
        if (!ReadExact(stream, lengthBuffer, 0, lengthBuffer.Length))
        {
            // Client disconnected
            return null;
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
        if (!ReadExact(stream, buffer, 0, messageLength))
        {
            // Client disconnected mid-message
            return null;
        }

        return Encoding.UTF8.GetString(buffer, 0, messageLength);
    }

    // Reads exactly 'count' bytes into buffer unless the client disconnects.
    // Returns false if the client disconnected before all bytes were read.
    static bool ReadExact(NetworkStream stream, byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (read == 0)
            {
                // Client disconnected
                return false;
            }

            totalRead += read;
        }

        return true;
    }
}
