using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace PongServer
{
    public class Ball
    {
        public double X { get; set;  }
        public double Y { get; set; }
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
        
        public double Radius { get; set; }

        public Ball(double startX, double startY, double startVelocityX, double startVelocityY, double radius)
        {
            X = startX;
            Y =  startY;
            VelocityX = startVelocityX;
            VelocityY = startVelocityY;
            Radius = radius;
        }

        public void Move()
        {
            X += VelocityX;
            Y += VelocityY;
        }
    }
    
    
    public class PongServer
    {
        private const int Port = 6049;

        // Keeps track of Client IP and ports
        private readonly Dictionary<string, TcpClient> _players = new();
        
        private bool _gameFull = false;
        private bool _gameStarted = false;
        private bool _gamePaused = false;

        
        // Game Variables
        private TcpClient? _player1Client;
        private TcpClient? _player2Client;

        private double _player1Top = 300;
        private double _player2Top = 300;

        private int _player1Score = 0;
        private int _player2Score = 0;
        
        private Ball _ball = new Ball(
            startX: 640,
            startY: 360,
            startVelocityX: 6,
            startVelocityY: 4,
            radius: 30
        );
        
        public static async Task Main(string[] args)
        {
            var server = new PongServer();
            await server.StartServer();
        }

        private async Task StartServer()
        {
            string localIp = "127.0.01";
            //string localIp = "10.26.22.218"; // SAU
            //string localIp = "172.20.10.3"; // hotspot 
            //string localIp = "172.16.1.53"; // CA
            Console.WriteLine($"Server started on {localIp}:{Port}");

            using var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();

            while (true)
            {
                var clientSocket = await listener.AcceptTcpClientAsync();
                _ = HandleConnectionAsync(clientSocket);
            }
        }

        public async Task HandleConnectionAsync(TcpClient clientSocket)
        {
            Console.WriteLine("player is trying to connect!");
            
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
                    
                    _ = Task.Run(GameLoop);

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
            _gamePaused = false;
            _player1Score = 0; // Keep score if only one player disconnects? Keep score in dictionary?
            _player2Score = 0;
            _player1Top = 300;
            _player2Top = 300;
        }

        private async Task HandleClientMessage(TcpClient clientSocket, string message)
        {
            if (!message.StartsWith("UPDATE:")) return;

            string direction = message.Substring("UPDATE:".Length);

            if (!_gameStarted) return;

            if (clientSocket == _player1Client)
            {
                if (direction == "UP") _player1Top -= 35;
                else if (direction == "DOWN") _player1Top += 35;

                await BroadcastAsync($"PADDLE:LEFT:{_player1Top}");
            }
            else if (clientSocket == _player2Client)
            {
                if (direction == "UP") _player2Top -= 35;
                else if (direction == "DOWN") _player2Top += 35;

                await BroadcastAsync($"PADDLE:RIGHT:{_player2Top}");
            }
        }

        private async Task BroadcastBallPosition()
        {
            if (_gamePaused)
            {
                _ball.X = 640;
                _ball.Y = 360;
            }
            
            await BroadcastAsync($"BALL:{_ball.X}:{_ball.Y}");
        }
        
        private async Task GameLoop()
        {
            const int tickRateMs = 16; //needs to be between 32 & 64

            while (_gameStarted)
            {
                if (!_gamePaused)
                {
                    await UpdateBall();
                    await BroadcastBallPosition();
                    await Task.Delay(tickRateMs);
                }

                if (_gamePaused)
                {
                    await Task.Delay(TimeSpan.FromSeconds(8));
                    _gamePaused = false;
                }
            }
        }

        private async Task UpdateScore(string player)
        {
            if (player == "PLAYER1")
                _player1Score++;
            else
                _player2Score++;
            
            await BroadcastAsync($"SCORE:{_player1Score}:{_player2Score}");
            await BroadcastBallPosition();
        }
        

        private async Task UpdateBall()
        {
            _ball.Move();

            if (_ball.Y <= 0 || _ball.Y >= 700)
            {
                _ball.VelocityY *= -1;
            }

            // Side wall collisions for testing
            /*
            if (_ball.X <= 0 || _ball.X >= 1250)
            {
                _ball.VelocityX *= -1;
            }
            */
            
            if (_ball.X - _ball.Radius <= 50) // paddle near x=50
            {
                if (_ball.Y >= _player1Top && _ball.Y <= _player1Top + 150) // paddle height 150
                {
                    _ball.VelocityX *= -1; // bounce
                }
                else
                { 
                    _gamePaused = true;
                    await UpdateScore("PLAYER2");
                }
            }

            if (_ball.X + _ball.Radius >= 1220) // paddle near x=1230 CHANGE ****
            {
                if (_ball.Y >= _player2Top && _ball.Y <= _player2Top + 150)
                {
                    _ball.VelocityX *= -1;
                }
                else
                {
                    _gamePaused = true;
                    await UpdateScore("PLAYER1");
                }
            }
        }
        
        private async Task BroadcastAsync(string message)
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

        private async Task SendToClientAsync(TcpClient client, string message)
        {
            if (!client.Connected) return;

            var stream = client.GetStream();
            byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();
        }
    }
}
