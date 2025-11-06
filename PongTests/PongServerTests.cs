using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace PongTests;

public class PongServerTests
{
    [Fact]
    public async Task PongServer_AssignsPlayer1Role_ToFirstClient()
    {
        // Arrange
        var server = new PongServer();
        var mockClient = CreateMockTcpClient();
        
        // Act
        _ = server.HandleConnectionAsync(mockClient);
        
        // Wait for the server to process
        await Task.Delay(300);
        
        // Assert - The server should have assigned Player1
        Assert.NotNull(server._player1Client);
        Assert.Equal(mockClient, server._player1Client);
    }

    [Fact]
    public async Task PongServer_AssignsPlayer2Role_ToSecondClient()
    {
        // Arrange
        var server = new PongServer();
        
        var mockClient1 = CreateMockTcpClient();
        var mockClient2 = CreateMockTcpClient();

        // Act - Connect first client
        var task1 = server.HandleConnectionAsync(mockClient1);
        await Task.Delay(200);

        // Connect second client
        var task2 = server.HandleConnectionAsync(mockClient2);
        await Task.Delay(200);

        // Assert
        Assert.NotNull(server._player1Client);
        Assert.NotNull(server._player2Client);
        Assert.Equal(mockClient1, server._player1Client);
        Assert.Equal(mockClient2, server._player2Client);
    }

    [Fact]
    public async Task PongServer_BroadcastsPaddlePositions_ToAllClients()
    {
        // Arrange
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var server = new PongServer();
        var client1 = new TcpClient();
        var client2 = new TcpClient();

        var client1Messages = new List<string>();
        var client2Messages = new List<string>();
        var messagesReceived = new TaskCompletionSource<bool>();

        // Server accepts and handles clients
        var serverTask = Task.Run(async () =>
        {
            var serverClient1 = await listener.AcceptTcpClientAsync();
            var serverClient2 = await listener.AcceptTcpClientAsync();
            
            _ = server.HandleConnectionAsync(serverClient1);
            _ = server.HandleConnectionAsync(serverClient2);
            
            await Task.Delay(500);
            
            // Broadcast paddle position
            await server.BroadcastAsync("PADDLE:LEFT:250");
        });

        // Connect clients
        await client1.ConnectAsync("127.0.0.1", port);
        await client2.ConnectAsync("127.0.0.1", port);

        // Start reading messages
        var readTask1 = Task.Run(async () =>
        {
            try
            {
                var reader = new StreamReader(client1.GetStream(), Encoding.UTF8);
                for (int i = 0; i < 5; i++)
                {
                    var line = await reader.ReadLineAsync();
                    if (line != null)
                    {
                        client1Messages.Add(line);
                        if (line.Contains("PADDLE:LEFT:250"))
                            break;
                    }
                }
            }
            catch { }
        });

        var readTask2 = Task.Run(async () =>
        {
            try
            {
                var reader = new StreamReader(client2.GetStream(), Encoding.UTF8);
                for (int i = 0; i < 5; i++)
                {
                    var line = await reader.ReadLineAsync();
                    if (line != null)
                    {
                        client2Messages.Add(line);
                        if (line.Contains("PADDLE:LEFT:250"))
                            break;
                    }
                }
            }
            catch { }
        });

        await serverTask;
        await Task.WhenAny(Task.WhenAll(readTask1, readTask2), Task.Delay(3000));

        // Assert - Both clients should receive the paddle message
        Assert.Contains(client1Messages, msg => msg.Contains("PADDLE:LEFT:250"));
        Assert.Contains(client2Messages, msg => msg.Contains("PADDLE:LEFT:250"));
        
        // Cleanup
        client1.Close();
        client2.Close();
        listener.Stop();
    }

    [Fact]
    public async Task PongServer_SendsCorrectRoleMessages_OnConnection()
    {
        // Arrange
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var server = new PongServer();
        
        // Create real clients for this test
        var client1 = new TcpClient();
        var client2 = new TcpClient();

        var client1Messages = new List<string>();
        var client2Messages = new List<string>();

        // Server accepts and handles clients
        var serverTask = Task.Run(async () =>
        {
            var serverClient1 = await listener.AcceptTcpClientAsync();
            var serverClient2 = await listener.AcceptTcpClientAsync();
            
            _ = server.HandleConnectionAsync(serverClient1);
            _ = server.HandleConnectionAsync(serverClient2);
            
            await Task.Delay(500);
        });

        // Act - Connect clients
        await client1.ConnectAsync("127.0.0.1", port);
        var reader1 = new StreamReader(client1.GetStream(), Encoding.UTF8);
        
        await client2.ConnectAsync("127.0.0.1", port);
        var reader2 = new StreamReader(client2.GetStream(), Encoding.UTF8);

        await serverTask;

        // Try to read messages with timeout
        var readTask1 = Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    var line = await reader1.ReadLineAsync();
                    if (line != null) client1Messages.Add(line);
                }
            }
            catch { }
        });

        var readTask2 = Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    var line = await reader2.ReadLineAsync();
                    if (line != null) client2Messages.Add(line);
                }
            }
            catch { }
        });

        await Task.WhenAny(Task.WhenAll(readTask1, readTask2), Task.Delay(2000));

        // Assert
        Assert.Contains(client1Messages, msg => msg.Contains("Player1"));
        Assert.Contains(client2Messages, msg => msg.Contains("Player2"));

        // Cleanup
        client1.Close();
        client2.Close();
        listener.Stop();
    }

    // Helper method to create a mock TcpClient
    private TcpClient CreateMockTcpClient()
    {
        // Create a pair of connected sockets
        var socket1 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var socket2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        
        // Bind and connect them locally
        var endpoint = new IPEndPoint(IPAddress.Loopback, 0);
        socket1.Bind(endpoint);
        socket1.Listen(1);
        
        var actualEndpoint = (IPEndPoint)socket1.LocalEndPoint;
        
        var connectTask = socket2.ConnectAsync(actualEndpoint);
        var acceptTask = socket1.AcceptAsync();
        
        Task.WaitAll(connectTask, acceptTask);
        
        var acceptedSocket = acceptTask.Result;
        
        // Create TcpClient from the accepted socket
        var client = new TcpClient();
        var clientProperty = typeof(TcpClient).GetProperty("Client");
        clientProperty?.SetValue(client, acceptedSocket);
        
        return client;
    }

    private TcpClient CreateMockTcpClientWithCapture(List<string> capturedMessages)
    {
        var client = CreateMockTcpClient();
        
        // Start a background task to capture messages
        _ = Task.Run(async () =>
        {
            try
            {
                var stream = client.GetStream();
                var reader = new StreamReader(stream, Encoding.UTF8);
                while (client.Connected)
                {
                    var message = await reader.ReadLineAsync();
                    if (message != null)
                        capturedMessages.Add(message);
                    else
                        break;
                }
            }
            catch { }
        });
        
        return client;
    }
}
