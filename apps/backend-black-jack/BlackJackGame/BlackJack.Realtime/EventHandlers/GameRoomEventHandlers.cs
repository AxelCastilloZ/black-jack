// GameRoomEventHandlers.cs - En BlackJack.Realtime/EventHandlers/
using Microsoft.Extensions.Logging;
using BlackJack.Domain.Common;
using BlackJack.Services.Common;
using BlackJack.Realtime.Models;
using BlackJack.Realtime.Services;

namespace BlackJack.Realtime.EventHandlers;

#region PlayerJoinedRoomEventHandler

public class PlayerJoinedRoomEventHandler : IDomainEventHandler<PlayerJoinedRoomEvent>
{
    private readonly ISignalRNotificationService _notificationService;
    private readonly ILogger<PlayerJoinedRoomEventHandler> _logger;

    public PlayerJoinedRoomEventHandler(
        ISignalRNotificationService notificationService,
        ILogger<PlayerJoinedRoomEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Handle(PlayerJoinedRoomEvent domainEvent)
    {
        try
        {
            _logger.LogInformation("[EventHandler] Processing PlayerJoinedRoomEvent: Player {PlayerId} joined room {RoomCode}",
                domainEvent.PlayerId, domainEvent.RoomCode);

            var eventModel = new PlayerJoinedEventModel(
                RoomCode: domainEvent.RoomCode,
                PlayerId: domainEvent.PlayerId.Value,
                PlayerName: domainEvent.PlayerName,
                Position: domainEvent.Position,
                TotalPlayers: domainEvent.TotalPlayers,
                Timestamp: domainEvent.OccurredOn
            );

            // Notificar a todos en la sala
            await _notificationService.NotifyPlayerJoinedAsync(domainEvent.RoomCode, eventModel);

            // Actualizar lobby con nueva información de la sala
            await NotifyLobbyRoomUpdated(domainEvent.RoomCode);

            _logger.LogInformation("[EventHandler] PlayerJoinedRoomEvent processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EventHandler] Error processing PlayerJoinedRoomEvent: {Error}", ex.Message);
            // No re-throw para evitar interrumpir el flujo principal
        }
    }

    private async Task NotifyLobbyRoomUpdated(string roomCode)
    {
        try
        {
            // En una implementación completa, obtendrías el estado actualizado de la sala
            // y notificarías al lobby sobre el cambio
            await _notificationService.NotifyGroupAsync(
                HubMethodNames.Groups.LobbyGroup,
                "RoomUpdated",
                new { roomCode, action = "playerJoined", timestamp = DateTime.UtcNow }
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[EventHandler] Failed to notify lobby about room update: {Error}", ex.Message);
        }
    }
}

#endregion

#region PlayerLeftRoomEventHandler

public class PlayerLeftRoomEventHandler : IDomainEventHandler<PlayerLeftRoomEvent>
{
    private readonly ISignalRNotificationService _notificationService;
    private readonly ILogger<PlayerLeftRoomEventHandler> _logger;

    public PlayerLeftRoomEventHandler(
        ISignalRNotificationService notificationService,
        ILogger<PlayerLeftRoomEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Handle(PlayerLeftRoomEvent domainEvent)
    {
        try
        {
            _logger.LogInformation("[EventHandler] Processing PlayerLeftRoomEvent: Player {PlayerId} left room {RoomCode}",
                domainEvent.PlayerId, domainEvent.RoomCode);

            var eventModel = new PlayerLeftEventModel(
                RoomCode: domainEvent.RoomCode,
                PlayerId: domainEvent.PlayerId.Value,
                PlayerName: domainEvent.PlayerName,
                RemainingPlayers: domainEvent.RemainingPlayers,
                Timestamp: domainEvent.OccurredOn
            );

            // Notificar a todos en la sala
            await _notificationService.NotifyPlayerLeftAsync(domainEvent.RoomCode, eventModel);

            // Si no quedan jugadores, notificar al lobby que la sala se cerró
            if (domainEvent.RemainingPlayers == 0)
            {
                await _notificationService.NotifyRoomClosedAsync(domainEvent.RoomCode);
            }
            else
            {
                await NotifyLobbyRoomUpdated(domainEvent.RoomCode);
            }

            _logger.LogInformation("[EventHandler] PlayerLeftRoomEvent processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EventHandler] Error processing PlayerLeftRoomEvent: {Error}", ex.Message);
        }
    }

    private async Task NotifyLobbyRoomUpdated(string roomCode)
    {
        try
        {
            await _notificationService.NotifyGroupAsync(
                HubMethodNames.Groups.LobbyGroup,
                "RoomUpdated",
                new { roomCode, action = "playerLeft", timestamp = DateTime.UtcNow }
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[EventHandler] Failed to notify lobby about room update: {Error}", ex.Message);
        }
    }
}

#endregion

#region SpectatorJoinedEventHandler

public class SpectatorJoinedEventHandler : IDomainEventHandler<SpectatorJoinedEvent>
{
    private readonly ISignalRNotificationService _notificationService;
    private readonly ILogger<SpectatorJoinedEventHandler> _logger;

    public SpectatorJoinedEventHandler(
        ISignalRNotificationService notificationService,
        ILogger<SpectatorJoinedEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Handle(SpectatorJoinedEvent domainEvent)
    {
        try
        {
            _logger.LogInformation("[EventHandler] Processing SpectatorJoinedEvent: Spectator {SpectatorId} joined room {RoomCode}",
                domainEvent.SpectatorId, domainEvent.RoomCode);

            var spectatorModel = new SpectatorModel(
                PlayerId: domainEvent.SpectatorId.Value,
                Name: domainEvent.SpectatorName,
                JoinedAt: domainEvent.OccurredOn
            );

            await _notificationService.NotifySpectatorJoinedAsync(domainEvent.RoomCode, spectatorModel);

            _logger.LogInformation("[EventHandler] SpectatorJoinedEvent processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EventHandler] Error processing SpectatorJoinedEvent: {Error}", ex.Message);
        }
    }
}

#endregion

#region SpectatorLeftEventHandler

public class SpectatorLeftEventHandler : IDomainEventHandler<SpectatorLeftEvent>
{
    private readonly ISignalRNotificationService _notificationService;
    private readonly ILogger<SpectatorLeftEventHandler> _logger;

    public SpectatorLeftEventHandler(
        ISignalRNotificationService notificationService,
        ILogger<SpectatorLeftEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Handle(SpectatorLeftEvent domainEvent)
    {
        try
        {
            _logger.LogInformation("[EventHandler] Processing SpectatorLeftEvent: Spectator {SpectatorId} left room {RoomCode}",
                domainEvent.SpectatorId, domainEvent.RoomCode);

            var spectatorModel = new SpectatorModel(
                PlayerId: domainEvent.SpectatorId.Value,
                Name: domainEvent.SpectatorName,
                JoinedAt: DateTime.UtcNow // No tenemos el JoinedAt original, usamos timestamp actual
            );

            await _notificationService.NotifySpectatorLeftAsync(domainEvent.RoomCode, spectatorModel);

            _logger.LogInformation("[EventHandler] SpectatorLeftEvent processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EventHandler] Error processing SpectatorLeftEvent: {Error}", ex.Message);
        }
    }
}

#endregion

#region GameStartedEventHandler

public class GameStartedEventHandler : IDomainEventHandler<GameStartedEvent>
{
    private readonly ISignalRNotificationService _notificationService;
    private readonly ILogger<GameStartedEventHandler> _logger;

    public GameStartedEventHandler(
        ISignalRNotificationService notificationService,
        ILogger<GameStartedEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Handle(GameStartedEvent domainEvent)
    {
        try
        {
            _logger.LogInformation("[EventHandler] Processing GameStartedEvent: Game started in room {RoomCode}",
                domainEvent.RoomCode);

            var eventModel = new GameStartedEventModel(
                RoomCode: domainEvent.RoomCode,
                GameTableId: domainEvent.GameTableId,
                PlayerNames: domainEvent.PlayerNames,
                FirstPlayerTurn: domainEvent.FirstPlayerTurn.Value,
                Timestamp: domainEvent.OccurredOn
            );

            await _notificationService.NotifyGameStartedAsync(domainEvent.RoomCode, eventModel);

            // Notificar al lobby que la sala cambió de estado
            await _notificationService.NotifyGroupAsync(
                HubMethodNames.Groups.LobbyGroup,
                "RoomUpdated",
                new { roomCode = domainEvent.RoomCode, action = "gameStarted", timestamp = domainEvent.OccurredOn }
            );

            _logger.LogInformation("[EventHandler] GameStartedEvent processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EventHandler] Error processing GameStartedEvent: {Error}", ex.Message);
        }
    }
}

#endregion

#region TurnChangedEventHandler

public class TurnChangedEventHandler : IDomainEventHandler<TurnChangedEvent>
{
    private readonly ISignalRNotificationService _notificationService;
    private readonly ILogger<TurnChangedEventHandler> _logger;

    public TurnChangedEventHandler(
        ISignalRNotificationService notificationService,
        ILogger<TurnChangedEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Handle(TurnChangedEvent domainEvent)
    {
        try
        {
            _logger.LogInformation("[EventHandler] Processing TurnChangedEvent: Turn changed to player {PlayerId} in room {RoomCode}",
                domainEvent.CurrentPlayerId, domainEvent.RoomCode);

            var eventModel = new TurnChangedEventModel(
                RoomCode: domainEvent.RoomCode,
                CurrentPlayerId: domainEvent.CurrentPlayerId.Value,
                CurrentPlayerName: domainEvent.CurrentPlayerName,
                PreviousPlayerId: domainEvent.PreviousPlayerId?.Value,
                TurnIndex: domainEvent.TurnIndex,
                Timestamp: domainEvent.OccurredOn
            );

            await _notificationService.NotifyTurnChangedAsync(domainEvent.RoomCode, eventModel);

            _logger.LogInformation("[EventHandler] TurnChangedEvent processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EventHandler] Error processing TurnChangedEvent: {Error}", ex.Message);
        }
    }
}

#endregion

#region GameEndedEventHandler

public class GameEndedEventHandler : IDomainEventHandler<GameEndedEvent>
{
    private readonly ISignalRNotificationService _notificationService;
    private readonly ILogger<GameEndedEventHandler> _logger;

    public GameEndedEventHandler(
        ISignalRNotificationService notificationService,
        ILogger<GameEndedEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Handle(GameEndedEvent domainEvent)
    {
        try
        {
            _logger.LogInformation("[EventHandler] Processing GameEndedEvent: Game ended in room {RoomCode}",
                domainEvent.RoomCode);

            var results = domainEvent.Results.Select(r => new PlayerResultModel(
                PlayerId: r.PlayerId.Value,
                PlayerName: r.PlayerName,
                HandValue: r.HandValue,
                Won: r.Won,
                Winnings: r.Winnings.Amount,
                PayoutType: r.PayoutType.ToString(),
                FinalBalance: 0 // Esto requeriría obtener el balance actual del jugador
            )).ToList();

            var eventModel = new GameEndedEventModel(
                RoomCode: domainEvent.RoomCode,
                Results: results,
                DealerHandValue: domainEvent.DealerHandValue,
                WinnerId: domainEvent.WinnerId?.Value,
                Timestamp: domainEvent.OccurredOn
            );

            await _notificationService.NotifyGameEndedAsync(domainEvent.RoomCode, eventModel);

            // Notificar al lobby que la sala volvió al estado de espera
            await _notificationService.NotifyGroupAsync(
                HubMethodNames.Groups.LobbyGroup,
                "RoomUpdated",
                new { roomCode = domainEvent.RoomCode, action = "gameEnded", timestamp = domainEvent.OccurredOn }
            );

            _logger.LogInformation("[EventHandler] GameEndedEvent processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EventHandler] Error processing GameEndedEvent: {Error}", ex.Message);
        }
    }
}

#endregion