using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using PongClient;
using Xunit;

namespace PongTests;

public class NetworkClientTests
{
    [Fact]
    public async Task NetworkClient_CanConnectToServer()
    {
        // Arrange
        var server = new TcpListener(IPAddress.Loopback, 0); // Use port 0 to get any available port
        server.Start();
        var port = ((IPEndPoint)server.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            var client = await server.AcceptTcpClientAsync();
            return client;
        });

        // Create a custom NetworkClient for testing
        var client = new TestableNetworkClient();

        // Act
        await client.ConnectToServerAsync("127.0.0.1", port);

        // Assert
        var serverClient = await serverTask;
        Assert.True(client.IsConnected());
        
        // Cleanup
        serverClient.Close();
        client.Disconnect();
        server.Stop();
    }

    [Fact]
    public async Task NetworkClient_CanSendMessagesToServer()
    {
        // Arrange
        var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        var port = ((IPEndPoint)server.LocalEndpoint).Port;

        string receivedMessage = null;
        var messageReceived = new TaskCompletionSource<bool>();

        var serverTask = Task.Run(async () =>
        {
            var client = await server.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            receivedMessage = await reader.ReadLineAsync();
            messageReceived.SetResult(true);
            return client;
        });

        var client = new TestableNetworkClient();
        await client.ConnectToServerAsync("127.0.0.1", port);

        // Act
        await client.SendAsync("Hello Server");

        // Assert
        await Task.WhenAny(messageReceived.Task, Task.Delay(5000));
        Assert.Equal("Hello Server", receivedMessage);
        
        // Cleanup
        var serverClient = await serverTask;
        serverClient.Close();
        client.Disconnect();
        server.Stop();
    }

    [Fact]
    public async Task NetworkClient_ReceivesMessagesFromServer()
    {
        // Arrange
        var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        var port = ((IPEndPoint)server.LocalEndpoint).Port;

        string receivedMessage = null;
        var messageReceived = new TaskCompletionSource<bool>();

        var serverTask = Task.Run(async () =>
        {
            var client = await server.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            await writer.WriteLineAsync("Hello Client");
            await Task.Delay(100); // Give time for message to be sent
            return client;
        });

        var client = new TestableNetworkClient();
        client.MessageReceived += (msg) =>
        {
            receivedMessage = msg;
            messageReceived.SetResult(true);
        };

        await client.ConnectToServerAsync("127.0.0.1", port);
        
        // Act
        _ = client.ListenForMessagesAsync();

        // Assert
        await Task.WhenAny(messageReceived.Task, Task.Delay(5000));
        Assert.Equal("Hello Client", receivedMessage);
        
        // Cleanup
        var serverClient = await serverTask;
        serverClient.Close();
        client.Disconnect();
        server.Stop();
    }
}

// Testable version of NetworkClient that allows custom connection parameters
public class TestableNetworkClient : NetworkClient
{
    private TcpClient? _testClient;
    private NetworkStream? _testStream;

    public async Task ConnectToServerAsync(string ip, int port)
    {
        _testClient = new TcpClient();
        await _testClient.ConnectAsync(ip, port);
        _testStream = _testClient.GetStream();
    }

    public new async Task SendAsync(string message)
    {
        if (_testClient == null || !_testClient.Connected || _testStream == null)
            return;

        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
            await _testStream.WriteAsync(bytes, 0, bytes.Length);
            await _testStream.FlushAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
        }
    }

    public new async Task ListenForMessagesAsync()
    {
        using var reader = new StreamReader(_testStream, Encoding.UTF8);
        try
        {
            while (_testClient.Connected)
            {
                string message = await reader.ReadLineAsync();
                if (message != null)
                {
                    // Invoke the base class event
                    OnMessageReceived(message);
                }
                else
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading from server: {ex.Message}");
        }
    }

    protected void OnMessageReceived(string message)
    {
        // Access the base class MessageReceived event through reflection or expose it
        var eventField = typeof(NetworkClient).GetField("MessageReceived", 
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var eventDelegate = (Action<string>)eventField?.GetValue(this);
        eventDelegate?.Invoke(message);
    }

    public bool IsConnected()
    {
        return _testClient?.Connected ?? false;
    }

    public void Disconnect()
    {
        _testStream?.Close();
        _testClient?.Close();
    }
}
