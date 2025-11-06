using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("PongTests")]


public class PongServer
{
    // Manage Game Connections
    private readonly Dictionary<string, TcpClient> _players = new();
    private bool _gameFull = false;
    private bool _gameStarted = false;
    
    internal TcpClient? _player1Client;
    internal TcpClient? _player2Client;
    
    private double _player1Top = 300;
    private double _player2Top = 300;
    
    private int _player1Score = 0;
    private int _player2Score = 0;

    
    
    static async Task Main(string[] args) // Dont need args?
    {
        var server = new PongServer();
        await server.StartServer();
    }

    internal async Task BroadcastAsync(string message)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(message + "\n");

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

    internal async Task SendtoClientAsync(TcpClient client, string message)
    {
        if (client.Connected)
        {
            var stream = client.GetStream();
            byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();
        }
    }
    
    internal async Task HandleConnectionAsync(TcpClient clientSocket)
    {
        var clientEndPoint = clientSocket.Client.RemoteEndPoint.ToString();
        _players[clientEndPoint] = clientSocket;
        
        Console.WriteLine($"Client connected: {clientEndPoint}");
        if (!_gameFull)
        {
            if (_player1Client == null)
            {
                _player1Client = clientSocket;
                await SendtoClientAsync(clientSocket, "YouAre:Player1");
                await BroadcastAsync("Player1Connected");
                
                if (_player2Client != null)
                    await SendtoClientAsync(clientSocket, "Player2Connected");
                
            }
            else if (_player2Client == null)
            {
                _player2Client = clientSocket;
                await SendtoClientAsync(clientSocket, "YouAre:Player2");
                await SendtoClientAsync(clientSocket, "Player1Connected");
                await BroadcastAsync("Player2Connected");
                Thread.Sleep(1000);
                await BroadcastAsync("STARTGAME");
                _gameStarted = true;
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
                
                if (message.StartsWith("UPDATE:"))
                {
                    string direction = message.Substring("UPDATE:".Length);

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

        _gameFull = false;
    }
    
    private async Task StartServer()
    {
        var ip =  "10.26.22.218"; // Macbook IP 
        var port = 6049;
        bool finished = false;

        using var serverSocket = new TcpListener(IPAddress.Any, port);
        serverSocket.Start();
        Console.WriteLine($"Server started on {ip}:{port}");
        
        while (!finished)
        {
            try
            {
                var clientSocket = await serverSocket.AcceptTcpClientAsync();
                _ = HandleConnectionAsync(clientSocket);
            }
            catch (SocketException)
            {
                finished = true;
            }
        }
        Console.WriteLine("Server is stopping");
    }
}