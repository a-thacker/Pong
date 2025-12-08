using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace PongClient;

public partial class MainWindow : Window
{
    private readonly NetworkClient _networkClient;

    //private readonly string _serverIp = "10.0.0.81";
    /* INCLUDE YOUR IP
    
    */
    
    private string _serverIp = "10.26.21.55"; 
    
    
    private bool _gameStarted;
    
    private readonly double _leftTop = 300;
    private readonly double _rightTop = 300;
    
    private Rectangle? _leftPaddle;
    private Rectangle? _rightPaddle;
    private Ellipse? _ball;
    
    private string? _playerId = "";
    
    private string _player1Score = "0";
    private string _player2Score = "0";
    private bool _winner;

    public MainWindow()
    {
        InitializeComponent();

        _networkClient = new NetworkClient();

        _networkClient.MessageReceived += OnServerMessage;

        _ = ConnectAndListenAsync();

        InitializePaddles();

        this.KeyDown += OnKeyDown;

    }

    private async Task ConnectAndListenAsync()
    {
        await _networkClient.ConnectAsync(_serverIp);
        _ = _networkClient.ListenForMessagesAsync();
    }

    private void InitializePaddles()
    {
        _leftPaddle = new Rectangle { Width = 20, Height = 100, Fill = Brushes.White };
        _rightPaddle = new Rectangle { Width = 20, Height = 100, Fill = Brushes.White };

        GameCanvas.Children.Add(_leftPaddle);
        GameCanvas.Children.Add(_rightPaddle);

        Canvas.SetLeft(_leftPaddle, 50);
        Canvas.SetRight(_rightPaddle, 50);

        Canvas.SetTop(_leftPaddle, _leftTop);
        Canvas.SetTop(_rightPaddle, _rightTop);
    }

    private void CreateBall()
    {
        _ball = new Ellipse
        {
            Width = 30,
            Height = 30,
            Fill = Brushes.Black
        };
        Canvas.SetLeft(_ball, 615);
        Canvas.SetTop(_ball, 342);
        GameCanvas.Children.Add(_ball);
    }
    
    private void UpdateBallPosition(int x, int y)
    {
        if (_ball == null)
            return;
        
        Canvas.SetLeft(_ball, x);
        Canvas.SetTop(_ball, y);
    }
    
    private async void OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        string keyToSend = "None";

        if (e.Key == Avalonia.Input.Key.Up)
        {
            keyToSend = $"UPDATE:UP";
        }
        else if (e.Key == Avalonia.Input.Key.Down)
        {
            keyToSend = $"UPDATE:DOWN";
        }

        await _networkClient.SendAsync(keyToSend);
    }
    
    private async Task StartSequence()
    {
        TimerTitle.Text = "Starting in 3";
        await Task.Delay(TimeSpan.FromSeconds(1));
        TimerTitle.Text = "Starting in 2";
        await Task.Delay(TimeSpan.FromSeconds(1));
        TimerTitle.Text = "Starting in 1";
        await Task.Delay(TimeSpan.FromSeconds(1));
        TimerTitle.Text = "GO!";
        await Task.Delay(TimeSpan.FromSeconds(1));
        TimerTitle.Text = "";
        _gameStarted = true;
        
        if (_ball == null)
            CreateBall();
        
        if ( _ball != null)
            _ball.Fill = Brushes.White;
    }
    
    private async Task ScoreSequence(string playerScored)
    {
        if (_ball != null)
            _ball.Fill = Brushes.Black;
        
        TimerTitle.Text = $"{playerScored} scored!";
        await Task.Delay(TimeSpan.FromSeconds(3));
        TimerTitle.Text = "";
        if (!_winner)
        {
            await StartSequence();
            await Task.Delay(TimeSpan.FromSeconds(4));
        }
    }
    
    private async void OnServerMessage(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(async() =>
        {
            if (message.StartsWith("STARTGAME"))
            {
                Console.WriteLine($"Canvas actual size: {GameCanvas.Bounds.Width} x {GameCanvas.Bounds.Height}");
                await StartSequence();
            }

            if (_gameStarted && message.StartsWith("BALL:"))
            {
                string[] parts = message.Split(':');
                int x = int.Parse(parts[1]);
                int y = int.Parse(parts[2]);
                
                UpdateBallPosition(x, y);
            }
            
            if (message.StartsWith("WINNER"))
            {
                _winner = true;
                string[] parts = message.Split(':');
                TimerTitle.Text = $"{parts[1]} WINS!";
            }
            
            else if (_gameStarted && message.StartsWith("SCORE:"))
            {
                string[] parts = message.Split(':');
                string player1ScoreFromServer = parts[1];
                string player2ScoreFromServer = parts[2];

                if (player1ScoreFromServer != _player1Score)
                {
                    _player1Score = player1ScoreFromServer;
                    Player1Score.Text = _player1Score;
                    await ScoreSequence("Player 1");
                }
                else if (player2ScoreFromServer != _player2Score)
                {
                    _player2Score = player2ScoreFromServer;
                    Player2Score.Text = _player2Score;
                    await ScoreSequence("Player 2");
                }

            }
            
            if (_gameStarted && message.StartsWith("PADDLE:"))
            {
                string[] parts = message.Split(':');
                if (parts.Length < 3) return;
                
                string side = parts[1];
                double newTop = double.Parse(parts[2]);

                
                
                //Keep this version of hte if-else loop
                if (side == "LEFT" && _leftPaddle != null)
                    Canvas.SetTop(_leftPaddle, newTop);
                else if (side != "LEFT" && _rightPaddle != null)
                    Canvas.SetTop(_rightPaddle, newTop);

                //Canvas.SetTop(side == "LEFT" ? _leftPaddle : _rightPaddle, newTop);
                
                /*
                if (side == "LEFT")
                    Canvas.SetTop(_leftPaddle, newTop);
                else
                    Canvas.SetTop(_rightPaddle, newTop);
                    */
            }
            
            if (message.StartsWith("YouAre:"))
            {
                _playerId = message.Substring("YouAre:".Length);
                Console.WriteLine($"You are {_playerId} ✅");
                Title = $"Pong Client - {_playerId}";
            }
            else if (message.Contains("Player1Connected"))
            {
                Player1Status.Text = "Player 1: Connected";
            }
            else if (message.Contains("Player2Connected"))
            {
                Player2Status.Text = "Player 2: Connected";
            }
            else if (message.Contains("Player1Disconnected"))
            {
                Player1Status.Text = "Player 1: Waiting...";
                _gameStarted = false;
            }
            else if (message.Contains("Player2Disconnected"))
            {
                Player2Status.Text = "Player 2: Waiting...";
                _gameStarted = false;
            }
            else if (message.Contains("StartGame"))
            {
                Console.WriteLine("Game Starting!");
            }
        });
    }
}

