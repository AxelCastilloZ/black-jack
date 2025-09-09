namespace Blackjack.Application.Interfaces;

public interface IGameService
{
    Task<object> GetGameStateAsync(int roomId);
    Task<bool> StartBettingAsync(int roomId);
    Task<bool> ResetGameAsync(int roomId);
}
