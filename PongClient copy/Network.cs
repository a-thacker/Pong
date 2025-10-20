using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PongClient
{
    public class NetworkClient
    {
        private TcpClient client;

        public async Task ConnectAsync()
        {
            client = new TcpClient();

            string serverIP = GetLocalIPAddress();
            await client.ConnectAsync(serverIP, 6049);

            Console.WriteLine("Connected to server!");
        }

        public TcpClient GetClient() => client;
        
        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }

            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        private async Task HandleConnectionAsync(TcpClient clientSocket)
        {
            var clientEndPoint = clientSocket.Client.RemoteEndPoint;
            Console.WriteLine($"Client connected: {clientEndPoint}");

        }
    }
}