namespace black_jack_backend.Common.Interfaces;

public interface IGameEvents
{
    // Player management events
    Task PlayerJoined(string roomId, string playerName, int seatIndex);
    Task PlayerLeft(string roomId, string playerName, int seatIndex);
    
    // Game state events
    Task TurnChanged(string roomId, string nextPlayerName, int seatIndex);
    Task GameUpdated(string roomId, object gameState);
    Task GameEnded(string roomId, object results);
    
    // Betting events
    Task BetPlaced(string roomId, int seatIndex, decimal amount);
    Task BettingPhaseStarted(string roomId);
    
    // Game action events
    Task CardDealt(string roomId, int seatIndex, string card, int newTotal);
    Task PlayerAction(string roomId, int seatIndex, string action, object details);
    
    // Room events
    Task RoomCreated(string roomId, string roomName);
    Task RoomClosed(string roomId);
}