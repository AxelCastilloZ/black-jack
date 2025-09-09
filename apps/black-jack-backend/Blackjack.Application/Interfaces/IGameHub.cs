namespace black_jack_backend.Common.Interfaces;

public interface IGameHub
{
    // Client methods (called from server)
    Task JoinRoom(string roomId, string playerName);
    Task LeaveRoom(string roomId);
    Task SendGameAction(string roomId, string actionType, object payload);
    Task SendMessage(string roomId, string message);
}