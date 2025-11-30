using System;
using System.IO;
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

        private TcpClient? _client;
        private NetworkStream? _stream;
        private bool _isConnected;

        public event Action<string>? MessageReceived;
        
        public async Task ConnectAsync(string serverIp)
        {
            try
            {
                _client = new TcpClient();
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
    }
}