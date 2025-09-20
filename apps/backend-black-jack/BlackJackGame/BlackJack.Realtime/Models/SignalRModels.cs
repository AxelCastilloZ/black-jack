// BlackJack.Realtime/Models/SignalRModels.cs - ARCHIVO COMPLETO CON TODOS LOS MODELOS
namespace BlackJack.Realtime.Models;

#region Response Wrappers

public record SignalRResponse<T>(
    bool Success,
    T? Data,
    string? Error,
    DateTime Timestamp
)
{
    public static SignalRResponse<T> Ok(T data) =>
        new(true, data, null, DateTime.UtcNow);

    public static SignalRResponse<T> Fail(string error) =>
        new(false, default, error, DateTime.UtcNow);
}

public record SignalRResponse(
    bool Success,
    string? Message,
    string? Error,
    DateTime Timestamp
)
{
    public static SignalRResponse Ok(string message) =>
        new(true, message, null, DateTime.UtcNow);

    public static SignalRResponse Fail(string error) =>
        new(false, null, error, DateTime.UtcNow);
}

#endregion

#region Request Models (Client -> Server)

public record CreateRoomRequest(
    string RoomName,
    int MaxPlayers = 6
);

public record JoinRoomRequest(
    string RoomCode,
    string PlayerName
);

public record JoinSeatRequest(
    string RoomCode,
    int Position
);

public record LeaveSeatRequest(
    string RoomCode
);

public record PlaceBetRequest(
    string RoomCode,
    decimal Amount
);

public record PlayerActionRequest(
    string RoomCode,
    string Action
);

#endregion

#region Response Models (Server -> Client)

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
    int Position, // -1 = no seat assigned
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

public record PlayerActionEventModel(
    string RoomCode,
    Guid PlayerId,
    string PlayerName,
    string Action,
    Guid HandId,
    int HandValue,
    List<object>? NewCards,
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

public record CardDealtEventModel(
    string RoomCode,
    Guid? PlayerId,
    string? PlayerName,
    object Card,
    Guid HandId,
    int NewHandValue,
    bool IsVisible,
    DateTime Timestamp
);

#endregion

#region Game Models

public record GameStateModel(
    string RoomCode,
    string Status,
    int RoundNumber,
    List<object> Players, // Simplificado por ahora
    object Dealer,
    string? CurrentPlayerTurn,
    bool CanPlaceBets,
    bool CanStartRound
);

#endregion

#region Chat Models

public record ChatMessageModel(
    Guid PlayerId,
    string PlayerName,
    string Message,
    DateTime Timestamp,
    string Type = "player"
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