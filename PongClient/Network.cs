using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PongClient
{
    public class NetworkClient
    {
        private TcpClient _client;
        private NetworkStream _stream;

        public event Action<string>? MessageReceived;

        public async Task ConnectAsync()
        {
            try
            {
                _client = new TcpClient();
                string serverIP = GetLocalIPAddress();

                await _client.ConnectAsync(serverIP, 6049);
                _stream = _client.GetStream();

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

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
                await _stream.WriteAsync(bytes, 0, bytes.Length);
                await _stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }

        public async Task ListenForMessagesAsync()
        {
            using var reader = new StreamReader(_stream, Encoding.UTF8);
            try
            {
                while (_client.Connected)
                {
                    string? message = await reader.ReadLineAsync();
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
        }

        public TcpClient GetClient() => _client;

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
    }
}
