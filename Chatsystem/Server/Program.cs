using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
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

        // First message received is username
        byte[] buffer = new byte[1024];
        int byteCount = stream.Read(buffer, 0, buffer.Length);
        string username = Encoding.UTF8.GetString(buffer, 0, byteCount).Trim();

        Console.WriteLine($"{username} has joined the chat!");

        while (true)
        {
            try
            {
                byteCount = stream.Read(buffer, 0, buffer.Length);
                if (byteCount == 0)
                {
                    Console.WriteLine($"{username} disconnected.");
                    break;
                }

                string message = Encoding.UTF8.GetString(buffer, 0, byteCount).Trim();
                Console.WriteLine($"[{username}]: {message}");
            }
            catch
            {
                Console.WriteLine($"{username} disconnected unexpectedly.");
                break;
            }
        }

        client.Close();
    }
}
