using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace PongClient;

public partial class MainWindow : Window
{
    private NetworkClient _networkClient;
    private double Top = 300;
    private int Left = 50;

    public MainWindow()
    {
        InitializeComponent();
        _networkClient = new NetworkClient();
        _ = _networkClient.ConnectAsync();
        
        DrawPaddle();

        this.KeyDown += OnKeyDown;
        
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        timer.Tick += (s, e) => UpdateGame();
        timer.Start();
    }


    private void UpdateGame()
    {
    }
    
    private void DrawPaddle()
    {
        GameCanvas.Children.Clear();
        
        var paddle = new Rectangle
        {
            Width = 20,
            Height = 100,
            Fill = Brushes.White
        };

        Canvas.SetLeft(paddle, Left);
        Canvas.SetTop(paddle, Top);

        GameCanvas.Children.Add(paddle);
    }

    private async void OnKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Up)
        {
            Top -= 15;
            await SendKeyToServer("UP");
        }

        if (e.Key == Avalonia.Input.Key.Down)
        {
            Top += 15;
            await SendKeyToServer("Down");
        }
        
        DrawPaddle();
    }

    private async Task SendKeyToServer(string key)
    {
        var client = _networkClient.GetClient();
        if (client?.Connected == true)
        {
            var stream = client.GetStream();
            var bytes = System.Text.Encoding.UTF8.GetBytes(key);
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();
        }
    }
}
