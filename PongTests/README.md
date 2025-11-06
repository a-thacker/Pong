# Pong Game Unit Tests

This test project contains comprehensive unit tests for the Pong networked game, covering client-server communication, player management, and message handling.

## Test Coverage

### 1. NetworkClient Tests (`NetworkClientTests.cs`)

Tests for the client-side networking component:

- **NetworkClient_CanConnectToServer**: Verifies that the NetworkClient can successfully establish a TCP connection to a server.
- **NetworkClient_CanSendMessagesToServer**: Tests that the NetworkClient can send text messages to the server and the server receives them correctly.
- **NetworkClient_ReceivesMessagesFromServer**: Validates that the NetworkClient can receive messages from the server through its MessageReceived event.

### 2. PongServer Tests (`PongServerTests.cs`)

Tests for the server-side game logic:

- **PongServer_AssignsPlayer1Role_ToFirstClient**: Confirms that the first client to connect is assigned the Player1 role.
- **PongServer_AssignsPlayer2Role_ToSecondClient**: Confirms that the second client to connect is assigned the Player2 role.
- **PongServer_BroadcastsPaddlePositions_ToAllClients**: Verifies that when a paddle position update is broadcast, all connected clients receive the message.
- **PongServer_SendsCorrectRoleMessages_OnConnection**: Tests that clients receive proper role assignment messages ("YouAre:Player1" or "YouAre:Player2") when they connect.

### 3. Message Handling Tests (`MessageHandlingTests.cs`)

Protocol and message parsing tests:

- **MessageProtocol_PlayerRoleMessage_ParsesCorrectly**: Tests parsing of player role assignment messages (e.g., "YouAre:Player1").
- **MessageProtocol_PaddlePositionMessage_ParsesCorrectly**: Tests parsing of paddle position update messages (e.g., "PADDLE:LEFT:250").
- **MessageProtocol_PlayerConnectedMessage_IsValid**: Validates player connection status messages.
- **MessageProtocol_PlayerDisconnectedMessage_IsValid**: Validates player disconnection status messages.
- **MessageProtocol_StartGameMessage_IsValid**: Tests the game start message format.
- **MessageProtocol_PaddleMessages_ParseVariousValues**: Theory test with multiple data sets to validate paddle position parsing.
- **MessageProtocol_UpdateMessage_FormatsCorrectly**: Tests the format of client update messages (UP/DOWN commands).

## Running the Tests

```bash
# Run all tests
dotnet test PongTests/PongTests.csproj

# Run tests with detailed output
dotnet test PongTests/PongTests.csproj --verbosity normal

# Run a specific test
dotnet test PongTests/PongTests.csproj --filter "FullyQualifiedName~NetworkClient_CanConnectToServer"
```

## Test Architecture

### Testing Approach

1. **NetworkClient Tests**: Use real TCP connections with localhost to test actual network behavior.

2. **PongServer Tests**: Create isolated test servers with dynamic port allocation to avoid conflicts.

3. **Message Handling Tests**: Unit tests for protocol logic without network dependencies.

### Key Design Decisions

- **Real TCP Connections**: Tests use actual TCP sockets rather than mocks to ensure real-world behavior.
- **Dynamic Port Allocation**: Tests use port 0 to let the OS assign available ports, preventing conflicts.
- **Async/Await**: All network tests are asynchronous to properly test the async networking code.
- **Timeouts**: Tests include reasonable timeouts to prevent hanging on failures.

## Test Requirements

The tests require:
- .NET 9.0 SDK
- xUnit testing framework
- Access to localhost TCP connections
- No specific ports (uses dynamic port allocation)

## Notes on UI Testing

MainWindow UI tests were intentionally simplified to avoid Avalonia UI threading complexity. Instead:
- Message parsing logic is tested separately in `MessageHandlingTests`
- Protocol validation ensures the UI receives correct message formats
- Integration tests verify the full client-server communication flow

For production environments requiring full UI testing, consider:
- Using Avalonia.Headless with proper UI thread synchronization
- Extracting UI logic into testable view models (MVVM pattern)
- Using UI automation tools for end-to-end testing

## Code Coverage Summary

| Component | Coverage |
|-----------|----------|
| NetworkClient connection | ✓ |
| NetworkClient send/receive | ✓ |
| PongServer player assignment | ✓ |
| PongServer broadcasting | ✓ |
| Message protocol parsing | ✓ |
| UI message handling logic | ✓ (protocol level) |

## Future Enhancements

Potential additional tests:
- Connection failure scenarios
- Network timeout handling
- Multiple disconnection/reconnection cycles
- Concurrent message handling
- Invalid message format handling
- Large message payloads
- Security/authentication scenarios
