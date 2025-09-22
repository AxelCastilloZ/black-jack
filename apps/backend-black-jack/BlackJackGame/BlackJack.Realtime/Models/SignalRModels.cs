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

#region Auto-Betting Event Models

/// <summary>
/// Modelo para notificar el resultado completo del procesamiento de apuestas automáticas
/// </summary>
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

/// <summary>
/// Modelo para el resultado de apuesta automática de un jugador individual
/// </summary>
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

/// <summary>
/// Modelo para notificar cuando un jugador es removido de su asiento por fondos insuficientes
/// </summary>
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

/// <summary>
/// Modelo para notificar actualizaciones de balance de jugadores
/// </summary>
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

/// <summary>
/// Modelo para advertencias de fondos insuficientes
/// </summary>
public record InsufficientFundsWarningEventModel(
    string RoomCode,
    Guid PlayerId,
    string PlayerName,
    decimal CurrentBalance,
    decimal RequiredAmount,
    decimal DeficitAmount,
    int RoundsRemaining, // Cuántas rondas más puede costear el jugador
    bool WillBeRemovedNextRound,
    DateTime WarningTime
);

/// <summary>
/// Modelo para estadísticas de auto-betting de una sala
/// </summary>
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

/// <summary>
/// Modelo para detalles de auto-betting por jugador
/// </summary>
public record PlayerAutoBetDetailModel(
    Guid PlayerId,
    string PlayerName,
    int SeatPosition,
    decimal CurrentBalance,
    bool CanAffordBet,
    decimal BalanceAfterBet,
    int RoundsAffordable // Cuántas rondas más puede costear
);

/// <summary>
/// Modelo para notificar el inicio del procesamiento de apuestas automáticas
/// </summary>
public record AutoBetProcessingStartedEventModel(
    string RoomCode,
    int SeatedPlayersCount,
    decimal MinBetPerRound,
    decimal TotalBetAmount,
    DateTime StartedAt
);

/// <summary>
/// Modelo para validación de auto-betting de una sala
/// </summary>
public record AutoBetValidationEventModel(
    string RoomCode,
    bool IsValid,
    List<string> ErrorMessages,
    string RoomStatus,
    int SeatedPlayersCount,
    decimal MinBetPerRound,
    DateTime ValidatedAt
);

/// <summary>
/// Modelo para notificar fallos en el procesamiento de auto-betting
/// </summary>
public record AutoBetFailedEventModel(
    string RoomCode,
    string ErrorMessage,
    string ErrorCode,
    int AffectedPlayersCount,
    List<Guid> AffectedPlayerIds,
    DateTime FailedAt,
    bool RequiresManualIntervention
);

/// <summary>
/// Modelo para notificar cuando se configura/cambia la apuesta mínima por ronda
/// </summary>
public record MinBetPerRoundUpdatedEventModel(
    string RoomCode,
    decimal PreviousMinBet,
    decimal NewMinBet,
    Guid UpdatedByPlayerId,
    string UpdatedByPlayerName,
    DateTime UpdatedAt
);

/// <summary>
/// Modelo para notificar el resumen de una ronda de apuestas automáticas
/// </summary>
public record AutoBetRoundSummaryEventModel(
    string RoomCode,
    int RoundNumber,
    DateTime RoundStartedAt,
    DateTime RoundCompletedAt,
    TimeSpan ProcessingDuration,
    AutoBetProcessedEventModel Results,
    List<string> Notifications // Mensajes adicionales para mostrar al usuario
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

/// <summary>
/// Estado detallado de sala del jugador para reconexión robusta
/// </summary>
public record PlayerRoomState(
    Guid PlayerId,
    string RoomCode,
    string? TableId,
    List<string> Groups,
    DateTime LastActivity,
    DateTime? ConnectionLostAt
);

/// <summary>
/// Estadísticas del ConnectionManager para monitoreo
/// </summary>
public record ConnectionManagerStats
{
    public int TotalConnections { get; init; }
    public int OnlinePlayers { get; init; }
    public int PendingReconnections { get; init; }
    public int PlayersWithRoomState { get; init; }
    public DateTime Timestamp { get; init; }
}

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