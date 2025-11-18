using System;
using System.Net.Sockets;
using System.Text;

class Client
{
    static void Main()
    {
        TcpClient client = new TcpClient("127.0.0.1", 5000);
        NetworkStream stream = client.GetStream();

        Console.Write("Enter username: ");
        string username = Console.ReadLine();
        SendMessage(stream, username);

        while (true)
        {
            string message = Console.ReadLine();
            SendMessage(stream, message);
        }
    }

    static void SendMessage(NetworkStream stream, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        stream.Write(data, 0, data.Length);
    }
}
