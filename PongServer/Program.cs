using System.Net;
using System.Net.Sockets;
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

        public Ball(
            double startX,
            double startY,
            double startVelocityX,
            double startVelocityY,
            double radius)
        
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
        
        private bool _gameFull;
        private bool _gameStarted;
        private bool _gamePaused;

        
        // Authoritative Game Variables
        private TcpClient? _player1Client;
        private TcpClient? _player2Client;

        private double _player1Top = 300;
        private double _player2Top = 300;

        private int _player1Score;
        private int _player2Score;
        
        private readonly Ball _ball = new Ball(
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
            string localIp = "10.0.0.81";
            //string localIp = "10.26.22.218"; // SAU
            Console.WriteLine($"Server started on {localIp}:{Port}");

            using var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();

            while (true)
            {
                var clientSocket = await listener.AcceptTcpClientAsync();
                _ = HandleConnectionAsync(clientSocket);
            }
        }

        private async Task HandleConnectionAsync(TcpClient clientSocket)
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

            // Reset game state variables so it can restart cleanly
            RestartGame();

        }

        private async Task HandleClientMessage(TcpClient clientSocket, string message)
        {
            if (!message.StartsWith("UPDATE:")) return;

            string direction = message.Substring("UPDATE:".Length);

            if (_gamePaused) return;

            if (clientSocket == _player1Client)
            {
                double next = _player1Top;

                if (direction == "UP") next -= 35;
                else if (direction == "DOWN") next += 35;
                
                
                if (next > -100 && next < 820)
                {
                    _player1Top = next;
                }

                await BroadcastAsync($"PADDLE:LEFT:{_player1Top}");
            }
            else if (clientSocket == _player2Client)
            {
                double next = _player2Top;

                if (direction == "UP") next -= 35;
                else if (direction == "DOWN") next += 35;

                if (next > -100 && next < 820)
                {
                    _player2Top = next;
                }

                await BroadcastAsync($"PADDLE:RIGHT:{_player2Top}");
            }
        }

        private async void RestartGame()
        {
            _gamePaused = false;
            _player1Score = 0;
            _player2Score = 0;
            _player1Top = 300;
            _player2Top = 300;
            
            _ball.X = 640;
            _ball.Y = 360;

            _gameStarted = true;
            await BroadcastAsync("SCORE:0:0");
            await BroadcastBallPosition();
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
            const int tickRateMs = 20;

            while (true)
            {
                if (_gameStarted && !_gamePaused)
                {
                    await UpdateBall();
                    await BroadcastBallPosition();
                    await Task.Delay(tickRateMs);
                }
                else
                {
                    await Task.Delay(50);
                }

                if (_gamePaused)
                {
                    await Task.Delay(TimeSpan.FromSeconds(7));
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
            
            if (_player1Score == 2)
            {
                RestartGame();
                await BroadcastAsync("WINNER:Player 1");
                return;
            }
            else if (_player2Score == 2)
            {
                RestartGame();
                await BroadcastAsync("WINNER:Player 2");
                return;
            }
            
            _player1Top = 300;
            _player2Top = 300;
            await BroadcastAsync($"PADDLE:LEFT:{_player1Top}");
            await BroadcastAsync($"PADDLE:RIGHT:{_player1Top}");
            
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
            
            if (_ball.X - _ball.Radius <= 50)
            {
                if (_ball.Y >= _player1Top && _ball.Y <= _player1Top + 150)
                {
                    _ball.VelocityX *= -1;
                }
                else if (_ball.X - _ball.Radius < -50)
                { 
                    _gamePaused = true;
                    await UpdateScore("PLAYER2");
                }
            }

            if (_ball.X + _ball.Radius >= 1220) // paddle near x=1230 CHANGE
            {
                if (_ball.Y >= _player2Top && _ball.Y <= _player2Top + 150)
                {
                    _ball.VelocityX *= -1;
                }
                else if (_ball.X + _ball.Radius > 1310)
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
