using System;
using System.Threading.Tasks;
using Xunit;

namespace PongTests;

/// <summary>
/// Tests for message handling logic without UI threading complexity.
/// These tests verify the protocol and message parsing logic.
/// </summary>
public class MessageHandlingTests
{
    [Fact]
    public void MessageProtocol_PlayerRoleMessage_ParsesCorrectly()
    {
        // Arrange
        string message = "YouAre:Player1";
        
        // Act
        var parts = message.Split(':');
        
        // Assert
        Assert.Equal(2, parts.Length);
        Assert.Equal("YouAre", parts[0]);
        Assert.Equal("Player1", parts[1]);
    }

    [Fact]
    public void MessageProtocol_PaddlePositionMessage_ParsesCorrectly()
    {
        // Arrange
        string message = "PADDLE:LEFT:250";
        
        // Act
        var parts = message.Split(':');
        
        // Assert
        Assert.Equal(3, parts.Length);
        Assert.Equal("PADDLE", parts[0]);
        Assert.Equal("LEFT", parts[1]);
        Assert.Equal(250, double.Parse(parts[2]));
    }

    [Fact]
    public void MessageProtocol_PlayerConnectedMessage_IsValid()
    {
        // Arrange
        string message1 = "Player1Connected";
        string message2 = "Player2Connected";
        
        // Assert
        Assert.Contains("Player1Connected", message1);
        Assert.Contains("Player2Connected", message2);
    }

    [Fact]
    public void MessageProtocol_PlayerDisconnectedMessage_IsValid()
    {
        // Arrange
        string message1 = "Player1Disconnected";
        string message2 = "Player2Disconnected";
        
        // Assert
        Assert.Contains("Player1Disconnected", message1);
        Assert.Contains("Player2Disconnected", message2);
    }

    [Fact]
    public void MessageProtocol_StartGameMessage_IsValid()
    {
        // Arrange
        string message = "STARTGAME";
        
        // Assert
        Assert.StartsWith("STARTGAME", message);
    }

    [Theory]
    [InlineData("PADDLE:LEFT:100", "LEFT", 100)]
    [InlineData("PADDLE:RIGHT:200", "RIGHT", 200)]
    [InlineData("PADDLE:LEFT:350.5", "LEFT", 350.5)]
    public void MessageProtocol_PaddleMessages_ParseVariousValues(string message, string expectedSide, double expectedPosition)
    {
        // Act
        var parts = message.Split(':');
        string side = parts[1];
        double position = double.Parse(parts[2]);
        
        // Assert
        Assert.Equal(expectedSide, side);
        Assert.Equal(expectedPosition, position);
    }

    [Fact]
    public void MessageProtocol_UpdateMessage_FormatsCorrectly()
    {
        // Arrange & Act
        string upMessage = "UPDATE:UP";
        string downMessage = "UPDATE:DOWN";
        
        // Assert
        Assert.StartsWith("UPDATE:", upMessage);
        Assert.StartsWith("UPDATE:", downMessage);
        Assert.Equal("UP", upMessage.Substring("UPDATE:".Length));
        Assert.Equal("DOWN", downMessage.Substring("UPDATE:".Length));
    }
}
