using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("PongTests")]

namespace PongServer
{
    public class PongServer
    {
        private const int Port = 6049;
        private const int BroadcastPort = 6050;

        // Keeps track of Client IP and ports
        private readonly Dictionary<string, TcpClient> _players = new();
        
        private bool _gameFull = false;
        private bool _gameStarted = false;

        
        // Game Variables
        internal TcpClient? _player1Client;
        internal TcpClient? _player2Client;

        private double _player1Top = 300;
        private double _player2Top = 300;

        private int _player1Score = 0;
        private int _player2Score = 0;
        
        public static async Task Main(string[] args)
        {
            var server = new PongServer();
            await server.StartServer();
        }

        private async Task StartServer()
        {
            string localIp = GetLocalIPAddress();
            Console.WriteLine($"Server started on {localIp}:{Port}");

            // Optional: Broadcast server IP for auto-discovery by clients
            _ = Task.Run(() => BroadcastServerInfo(localIp));

            using var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();

            while (true)
            {
                var clientSocket = await listener.AcceptTcpClientAsync();
                _ = HandleConnectionAsync(clientSocket);
            }
        }

        internal async Task HandleConnectionAsync(TcpClient clientSocket)
        {
            // Store Client Connection in _players
            var clientEndPoint = clientSocket.Client.RemoteEndPoint?.ToString();
            _players[clientEndPoint] = clientSocket;
            Console.WriteLine($"Client connected: {clientEndPoint}");

            if (!_gameFull)
            {
                if (_player1Client == null)
                {
                    _player1Client = clientSocket;
                    await SendToClientAsync(clientSocket, "YouAre:Player1");
                    await BroadcastAsync("Player1Connected");

                    if (_player2Client != null)
                        await SendToClientAsync(clientSocket, "Player2Connected");
                }
                else if (_player2Client == null)
                {
                    _player2Client = clientSocket;
                    await SendToClientAsync(clientSocket, "YouAre:Player2");
                    await SendToClientAsync(clientSocket, "Player1Connected");
                    await BroadcastAsync("Player2Connected");
                }

                // Start the game when both players are connected
                if (_player1Client != null && _player2Client != null)
                {
                    _gameFull = true;
                    _gameStarted = true;

                    await Task.Delay(1000);
                    await BroadcastAsync("STARTGAME");
                    await Task.Delay(4000);
                }
            }

            await using var stream = clientSocket.GetStream();
            var buffer = new byte[1024];

            try
            {
                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    await HandleClientMessage(clientSocket, message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error with {clientEndPoint}: {ex.Message}");
            }

            // ==== CLIENT DISCONNECT ====
            clientSocket.Close();
            _players.Remove(clientEndPoint);
            Console.WriteLine($"Client disconnected: {clientEndPoint}");

            if (_player1Client == clientSocket)
            {
                _player1Client = null;
                await BroadcastAsync("Player1Disconnected");
            }

            if (_player2Client == clientSocket)
            {
                _player2Client = null;
                await BroadcastAsync("Player2Disconnected");
            }

            // Reset game state so it can restart cleanly
            _gameFull = false;
            _gameStarted = false;
        }

        private async Task HandleClientMessage(TcpClient clientSocket, string message)
        {
            if (!message.StartsWith("UPDATE:")) return;

            string direction = message.Substring("UPDATE:".Length);

            if (!_gameStarted) return;

            if (clientSocket == _player1Client)
            {
                if (direction == "UP") _player1Top -= 15;
                else if (direction == "DOWN") _player1Top += 15;

                await BroadcastAsync($"PADDLE:LEFT:{_player1Top}");
            }
            else if (clientSocket == _player2Client)
            {
                if (direction == "UP") _player2Top -= 15;
                else if (direction == "DOWN") _player2Top += 15;

                await BroadcastAsync($"PADDLE:RIGHT:{_player2Top}");
            }
        }

        internal async Task BroadcastAsync(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message + "\n");

            foreach (var player in _players.Values)
            {
                if (player.Connected)
                {
                    var stream = player.GetStream();
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                    await stream.FlushAsync();
                }
            }
        }

        internal async Task SendToClientAsync(TcpClient client, string message)
        {
            if (!client.Connected) return;

            var stream = client.GetStream();
            byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();
        }

        private async Task BroadcastServerInfo(string ip)
        {
            using var udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;

            var message = Encoding.UTF8.GetBytes($"PONG_SERVER:{ip}:{Port}");
            var endpoint = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);

            while (true)
            {
                await udpClient.SendAsync(message, message.Length, endpoint);
                await Task.Delay(2000);
            }
        }

        private string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }
            throw new Exception("No network adapters with an IPv4 address found.");
        }
    }
}