// SignalRModels.cs - En BlackJack.Realtime/Models/
using BlackJack.Domain.Enums;

namespace BlackJack.Realtime.Models;

#region Response Models (Server -> Client)

public record SignalRResponse<T>(bool Success, T? Data, string? Error, DateTime Timestamp)
{
    public static SignalRResponse<T> Ok(T data) => new(true, data, null, DateTime.UtcNow);
    public static SignalRResponse<T> Fail(string error) => new(false, default, error, DateTime.UtcNow);
}

public record SignalRResponse(bool Success, string? Message, string? Error, DateTime Timestamp)
{
    public static SignalRResponse Ok(string message) => new(true, message, null, DateTime.UtcNow);
    public static SignalRResponse Fail(string error) => new(false, null, error, DateTime.UtcNow);
}

#endregion

#region Room Models

public record RoomInfoModel(
    string RoomCode,
    string Name,
    string Status,
    int PlayerCount,
    int MaxPlayers,
    List<RoomPlayerModel> Players,
    List<SpectatorModel> Spectators,
    string? CurrentPlayerTurn,
    bool CanStart,
    DateTime CreatedAt
);

public record RoomPlayerModel(
    Guid PlayerId,
    string Name,
    int Position,
    bool IsReady,
    bool IsHost,
    bool HasPlayedTurn
);

public record SpectatorModel(
    Guid PlayerId,
    string Name,
    DateTime JoinedAt
);

public record ActiveRoomModel(
    string RoomCode,
    string Name,
    int PlayerCount,
    int MaxPlayers,
    string Status,
    DateTime CreatedAt
);

#endregion

#region Game Models

public record GameStateModel(
    string RoomCode,
    string Status,
    int RoundNumber,
    List<PlayerGameStateModel> Players,
    DealerGameStateModel Dealer,
    string? CurrentPlayerTurn,
    bool CanPlaceBets,
    bool CanStartRound
);

public record PlayerGameStateModel(
    Guid PlayerId,
    string Name,
    int Position,
    decimal Balance,
    decimal? CurrentBet,
    List<HandModel> Hands,
    bool IsActive,
    bool HasPlayedTurn
);

public record HandModel(
    Guid HandId,
    List<CardModel> Cards,
    int Value,
    string Status,
    bool IsSoft,
    bool IsBlackjack,
    bool IsBust
);

public record CardModel(
    string Rank,
    string Suit,
    int Value,
    string DisplayName,
    bool IsVisible = true
);

public record DealerGameStateModel(
    List<CardModel> Cards,
    int Value,
    string Status,
    bool HasHiddenCard
);

#endregion

#region Event Models

public record PlayerJoinedEventModel(
    string RoomCode,
    Guid PlayerId,
    string PlayerName,
    int Position,
    int TotalPlayers,
    DateTime Timestamp
);

public record PlayerLeftEventModel(
    string RoomCode,
    Guid PlayerId,
    string PlayerName,
    int RemainingPlayers,
    DateTime Timestamp
);

public record GameStartedEventModel(
    string RoomCode,
    Guid GameTableId,
    List<string> PlayerNames,
    Guid FirstPlayerTurn,
    DateTime Timestamp
);

public record TurnChangedEventModel(
    string RoomCode,
    Guid CurrentPlayerId,
    string CurrentPlayerName,
    Guid? PreviousPlayerId,
    int TurnIndex,
    DateTime Timestamp
);

public record CardDealtEventModel(
    string RoomCode,
    Guid? PlayerId, // null para dealer
    string? PlayerName,
    CardModel Card,
    Guid HandId,
    int NewHandValue,
    bool IsVisible,
    DateTime Timestamp
);

public record PlayerActionEventModel(
    string RoomCode,
    Guid PlayerId,
    string PlayerName,
    string Action,
    Guid HandId,
    int HandValue,
    List<CardModel>? NewCards,
    DateTime Timestamp
);

public record BetPlacedEventModel(
    string RoomCode,
    Guid PlayerId,
    string PlayerName,
    decimal BetAmount,
    decimal NewBalance,
    DateTime Timestamp
);

public record GameEndedEventModel(
    string RoomCode,
    List<PlayerResultModel> Results,
    int DealerHandValue,
    Guid? WinnerId,
    DateTime Timestamp
);

public record PlayerResultModel(
    Guid PlayerId,
    string PlayerName,
    int HandValue,
    bool Won,
    decimal Winnings,
    string PayoutType,
    decimal FinalBalance
);

#endregion

#region Request Models (Client -> Server)

public record JoinRoomRequest(string RoomCode, string PlayerName);

public record CreateRoomRequest(string RoomName, int MaxPlayers = 6);

public record PlaceBetRequest(string RoomCode, decimal Amount);

public record PlayerActionRequest(string RoomCode, string Action);

public record SendMessageRequest(string RoomCode, string Message);

#endregion

#region Chat Models

public record ChatMessageModel(
    Guid PlayerId,
    string PlayerName,
    string Message,
    DateTime Timestamp,
    string Type = "player" // "player", "system", "admin"
);

#endregion

#region Connection Models

public record ConnectionInfo(
    string ConnectionId,
    Guid? PlayerId,
    string? UserName,
    List<string> Groups,
    DateTime ConnectedAt
);

public record ReconnectionInfo(
    Guid PlayerId,
    string? LastRoomCode,
    DateTime LastSeen,
    bool WasInGame
);

#endregion

#region Utility Models

public record ErrorModel(
    string Message,
    string? Code,
    object? Details,
    DateTime Timestamp
);

public record SuccessModel(
    string Message,
    object? Data,
    DateTime Timestamp
);

#endregion