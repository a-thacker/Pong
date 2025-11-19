using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace PongClient;

public partial class MainWindow : Window
{
    private NetworkClient _networkClient;
    private string _serverIp = "10.26.22.218";
    //private string _serverIp = "172.16.1.53";

    private bool _gameStarted = false;
    
    private double _leftTop = 300;
    private double _rightTop = 300;
    
    private Rectangle? _leftPaddle;
    private Rectangle? _rightPaddle;
    private Ellipse? _ball;
    
    private string? _playerId = "";
    
    private string _player1Score = "0";
    private string _player2Score = "0";

    public MainWindow()
    {
        InitializeComponent();

        _networkClient = new NetworkClient();

        _networkClient.MessageReceived += OnServerMessage;

        _ = ConnectAndListenAsync();

        InitializePaddles();

        this.KeyDown += OnKeyDown;

        
        // Does not need this timer. The game updates when an action is preformed and a message is sent from the server.
        /*
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        timer.Tick += (s, e) => UpdateGame();
        timer.Start();
        */
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
            Fill = Brushes.White
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

    private void RemoveBall()
    {
        if (_ball != null)
        {
            GameCanvas.Children.Remove(_ball);
            _ball = null;
        }
    }
    
    private async void StartSequence()
    {
        Console.WriteLine($"Canvas size: {GameCanvas.Bounds.Width} x {GameCanvas.Bounds.Height}");

        TimerTitle.Text = "Starting in";
        StartTimer.Text = "  3";
        await Task.Delay(TimeSpan.FromSeconds(1));
        StartTimer.Text = "  2";
        await Task.Delay(TimeSpan.FromSeconds(1));
        StartTimer.Text = "  1";
        await Task.Delay(TimeSpan.FromSeconds(1));
        StartTimer.Text = "GO!";
        await Task.Delay(TimeSpan.FromSeconds(1));
        TimerTitle.Text = "";
        StartTimer.Text = "";
        _gameStarted = true;
        CreateBall();
    }
    
    private async void ScoreSequence(string playerscored)
    {
        RemoveBall();
        TimerTitle.Text = $"Player {playerscored} scored!";
        await Task.Delay(TimeSpan.FromSeconds(4));
        TimerTitle.Text = "";
        await Task.Delay(TimeSpan.FromSeconds(1));
        //StartSequence();
    }
    
    
    private void OnServerMessage(string message)
    {
        Console.WriteLine(message);
        Dispatcher.UIThread.Post(() =>
        {
            if (message.StartsWith("STARTGAME"))
            {
                //Console.WriteLine("Playing start sequence!");
                //Console.WriteLine($"Canvas actual size: {GameCanvas.Bounds.Width} x {GameCanvas.Bounds.Height}");
                StartSequence();
            }

            if (_gameStarted && message.StartsWith("BALL:"))
            {
                string[] parts = message.Split(':');
                int x = int.Parse(parts[1]);
                int y = int.Parse(parts[2]);
                
                UpdateBallPosition(x, y);
            }
            
            if (_gameStarted && message.StartsWith("SCORE:"))
            {
                string[] parts = message.Split(':');
                string player1Scorefromserver = parts[1];
                string player2Scorefromserver = parts[2];

                if (player1Scorefromserver != _player1Score)
                {
                    _player1Score = player1Scorefromserver;
                    Player1Score.Text = _player1Score;
                    ScoreSequence("Player1");
                }
                else if (player2Scorefromserver != _player2Score)
                {
                    _player2Score = player2Scorefromserver;
                    Player2Score.Text = _player2Score;
                    ScoreSequence("Player2");
                }

            }
            
            if (_gameStarted && message.StartsWith("PADDLE:"))
            {
                string[] parts = message.Split(':');
                if (parts.Length < 3) return;
                
                string side = parts[1];
                double newTop = double.Parse(parts[2]);

                if (side == "LEFT")
                    Canvas.SetTop(_leftPaddle, newTop);
                else
                    Canvas.SetTop(_rightPaddle, newTop);
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

