using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("PongTests")]

namespace PongClient
{
    public class NetworkClient
    {
        private const int ServerPort = 6049;
        private const int BroadcastPort = 6050;

        private TcpClient? _client;
        private NetworkStream? _stream;
        private bool _isConnected = false;

        public event Action<string>? MessageReceived;
        
        public async Task ConnectAsync(string? serverIp = null)
        {
            try
            {
                _client = new TcpClient();

                if (string.IsNullOrEmpty(serverIp))
                {
                    Console.WriteLine("Searching for Pong server");
                    serverIp = await DiscoverServerAsync();
                }
                
                Console.WriteLine($"Connecting to server at {serverIp}:{ServerPort}");
                await _client.ConnectAsync(serverIp, ServerPort);
                _stream = _client.GetStream();
                _isConnected = true;

                Console.WriteLine("Connected to server!");

                _ = ListenForMessagesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect: {ex.Message}");
            }
        }

        public async Task SendAsync(string message)
        {
            if (_client == null || !_client.Connected || _stream == null)
                return;

            
            byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
            await _stream.WriteAsync(bytes, 0, bytes.Length);
            await _stream.FlushAsync();
            
        }
        
        public async Task ListenForMessagesAsync()
        {
            if (_stream == null) return;
            using var reader = new StreamReader(_stream, Encoding.UTF8);

            try
            {
                while (_isConnected && _client?.Connected == true)
                {
                    string? message = (await reader.ReadLineAsync())?.Trim();
                    if (message != null)
                    {
                        MessageReceived?.Invoke(message);
                    }
                    else
                    {
                        Console.WriteLine("Server disconnected.");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading from server: {ex.Message}");
            }

            _isConnected = false;
        }

        public TcpClient? GetClient() => _client;

        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }
            throw new Exception("No network adapters with an IPv4 address found.");
        }

        private async Task<string?> DiscoverServerAsync(int timeoutMs = 3000)
        {
            using var udpClient = new UdpClient(BroadcastPort);
            udpClient.Client.ReceiveTimeout = timeoutMs;
            
            Console.WriteLine($"Listening for UDP broadcast on port {BroadcastPort}...");
            var result = await udpClient.ReceiveAsync();
            string msg = Encoding.UTF8.GetString(result.Buffer);

            if (msg.StartsWith("PONG_SERVER:"))
            {
                string[] parts = msg.Split(':');
                if (parts.Length >= 3)
                { 
                    string ip = parts[1];
                    Console.WriteLine($"Found server at {ip}");
                    return ip;
                }
            }
            return null;
        }
    }
}