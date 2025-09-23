// BlackJack.Realtime/Models/SignalRModels.cs - Modelos consolidados
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

// === GameRoomHub Requests ===
public record CreateRoomRequest(string RoomName, int MaxPlayers = 6);
public record JoinRoomRequest(string RoomCode, string PlayerName);
public record JoinSeatRequest(string RoomCode, int Position);
public record LeaveSeatRequest(string RoomCode);

// === GameControlHub Requests ===
public record PlayerActionRequest(string RoomCode, string Action);
public record PlaceBetRequest(string RoomCode, decimal Amount);

// === LobbyHub Requests ===
public record QuickJoinRequest(string? PreferredRoomCode = null);

#endregion

#region Core Response Models

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
    DateTime CreatedAt,
    // Auto-betting fields
    decimal MinBetPerRound,
    bool AutoBettingActive
);

public record RoomPlayerModel(
    Guid PlayerId,
    string Name,
    int Position, // -1 = no seat assigned, 0-5 = seat position
    bool IsReady,
    bool IsHost,
    bool HasPlayedTurn,
    // Balance and betting fields
    decimal CurrentBalance,
    decimal TotalBetThisSession,
    bool CanAffordBet
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

#region Game Event Models

// === Basic Player Events ===
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

// === Game State Events ===
public record GameStartedEventModel(
    string RoomCode,
    Guid GameTableId,
    List<string> PlayerNames,
    Guid FirstPlayerTurn,
    DateTime Timestamp
);

public record GameEndedEventModel(
    string RoomCode,
    List<PlayerResultModel> Results,
    int DealerHandValue,
    Guid? WinnerId,
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

// === Game Action Events ===
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

public record BetPlacedEventModel(
    string RoomCode,
    Guid PlayerId,
    string PlayerName,
    decimal BetAmount,
    decimal NewBalance,
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

#region Auto-Betting Models (Consolidado)

// === Modelo principal de resultado de auto-betting ===
public record AutoBetProcessedEventModel(
    string RoomCode,
    int TotalPlayersProcessed,
    int SuccessfulBets,
    int FailedBets,
    int PlayersRemovedFromSeats,
    decimal TotalAmountProcessed,
    List<AutoBetPlayerResultModel> PlayerResults,
    DateTime ProcessedAt,
    bool HasErrors,
    decimal SuccessRate
);

public record AutoBetPlayerResultModel(
    Guid PlayerId,
    string PlayerName,
    int SeatPosition,
    string Status, // "BetDeducted", "InsufficientFunds", "RemovedFromSeat", "Failed"
    decimal OriginalBalance,
    decimal NewBalance,
    decimal BetAmount,
    string? ErrorMessage = null
);

// === Eventos específicos de auto-betting ===
public record PlayerBalanceUpdatedEventModel(
    string RoomCode,
    Guid PlayerId,
    string PlayerName,
    decimal PreviousBalance,
    decimal NewBalance,
    decimal AmountChanged,
    string ChangeReason, // "AutoBet", "Winnings", "Manual", etc.
    DateTime UpdatedAt
);

public record PlayerRemovedFromSeatEventModel(
    string RoomCode,
    Guid PlayerId,
    string PlayerName,
    int SeatPosition,
    decimal RequiredAmount,
    decimal AvailableBalance,
    string Reason,
    DateTime RemovedAt
);

public record InsufficientFundsWarningEventModel(
    string RoomCode,
    Guid PlayerId,
    string PlayerName,
    decimal CurrentBalance,
    decimal RequiredAmount,
    decimal DeficitAmount,
    int RoundsRemaining,
    bool WillBeRemovedNextRound,
    DateTime WarningTime
);

// === Estadísticas y control de auto-betting ===
public record AutoBetStatisticsEventModel(
    string RoomCode,
    decimal MinBetPerRound,
    int SeatedPlayersCount,
    decimal TotalBetPerRound,
    int PlayersWithSufficientFunds,
    int PlayersWithInsufficientFunds,
    decimal TotalAvailableFunds,
    int ExpectedSuccessfulBets,
    decimal ExpectedTotalDeduction,
    List<PlayerAutoBetDetailModel> PlayerDetails,
    DateTime CalculatedAt
);

public record PlayerAutoBetDetailModel(
    Guid PlayerId,
    string PlayerName,
    int SeatPosition,
    decimal CurrentBalance,
    bool CanAffordBet,
    decimal BalanceAfterBet,
    int RoundsAffordable
);

public record AutoBetProcessingStartedEventModel(
    string RoomCode,
    int SeatedPlayersCount,
    decimal MinBetPerRound,
    decimal TotalBetAmount,
    DateTime StartedAt
);

public record AutoBetFailedEventModel(
    string RoomCode,
    string ErrorMessage,
    string ErrorCode,
    int AffectedPlayersCount,
    List<Guid> AffectedPlayerIds,
    DateTime FailedAt,
    bool RequiresManualIntervention
);

public record MinBetPerRoundUpdatedEventModel(
    string RoomCode,
    decimal PreviousMinBet,
    decimal NewMinBet,
    Guid UpdatedByPlayerId,
    string UpdatedByPlayerName,
    DateTime UpdatedAt
);

public record AutoBetRoundSummaryEventModel(
    string RoomCode,
    int RoundNumber,
    DateTime RoundStartedAt,
    DateTime RoundCompletedAt,
    TimeSpan ProcessingDuration,
    AutoBetProcessedEventModel Results,
    List<string> Notifications
);

#endregion

#region Game State Models

public record GameStateModel(
    string RoomCode,
    string Status,
    int RoundNumber,
    List<object> Players,
    object? Dealer,
    string? CurrentPlayerTurn,
    bool CanPlaceBets,
    bool CanStartRound
);

public record HandStateModel(
    Guid HandId,
    List<CardModel> Cards,
    int Value,
    string Status,
    bool IsVisible
);

public record CardModel(
    string Suit,
    string Rank,
    int Value,
    bool IsVisible = true
);

public record PlayerGameStateModel(
    Guid PlayerId,
    string Name,
    int SeatPosition,
    List<HandStateModel> Hands,
    decimal CurrentBet,
    decimal Balance,
    bool HasPlayedTurn,
    string Status
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

public record PlayerRoomState(
    Guid PlayerId,
    string RoomCode,
    string? TableId,
    List<string> Groups,
    DateTime LastActivity,
    DateTime? ConnectionLostAt
);

public record ConnectionManagerStats
{
    public int TotalConnections { get; init; }
    public int OnlinePlayers { get; init; }
    public int PendingReconnections { get; init; }
    public int PlayersWithRoomState { get; init; }
    public DateTime Timestamp { get; init; }
}

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

#region Error and Success Models

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

#region Validation Extensions

public static class ModelValidationExtensions
{
    public static bool IsValidSeatPosition(this int position) => position >= -1 && position <= 5;

    public static bool IsValidRoomCode(this string? roomCode) =>
        !string.IsNullOrWhiteSpace(roomCode) && roomCode.Length <= 10;

    public static bool IsValidPlayerName(this string? playerName) =>
        !string.IsNullOrWhiteSpace(playerName) && playerName.Length <= 30;

    public static bool IsValidBetAmount(this decimal amount) => amount > 0 && amount <= 10000;

    public static bool IsActiveRoom(this RoomInfoModel room) =>
        room.Status != "Closed" && room.PlayerCount > 0;

    public static bool HasAvailableSeats(this RoomInfoModel room) =>
        room.Players.Count(p => p.Position >= 0) < room.MaxPlayers;

    public static bool CanAffordAutoBet(this RoomPlayerModel player, decimal minBetPerRound) =>
        player.CurrentBalance >= minBetPerRound;
}

#endregion

#region Enum Models

public static class GameStatuses
{
    public const string WaitingForPlayers = "WaitingForPlayers";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Paused = "Paused";
    public const string Cancelled = "Cancelled";
    public const string Closed = "Closed";
}

public static class PlayerActions
{
    public const string Hit = "Hit";
    public const string Stand = "Stand";
    public const string DoubleDown = "DoubleDown";
    public const string Split = "Split";
    public const string Surrender = "Surrender";
}

public static class HandStatuses
{
    public const string Active = "Active";
    public const string Busted = "Busted";
    public const string Blackjack = "Blackjack";
    public const string Completed = "Completed";
    public const string Surrendered = "Surrendered";
}

public static class AutoBetStatuses
{
    public const string BetDeducted = "BetDeducted";
    public const string InsufficientFunds = "InsufficientFunds";
    public const string RemovedFromSeat = "RemovedFromSeat";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}

#endregion