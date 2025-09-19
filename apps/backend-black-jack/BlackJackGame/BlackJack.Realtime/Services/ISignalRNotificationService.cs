// ISignalRNotificationService.cs - En BlackJack.Realtime/Services/
using BlackJack.Domain.Models.Users;
using BlackJack.Realtime.Models;

namespace BlackJack.Realtime.Services;

public interface ISignalRNotificationService
{
    #region Notificaciones de sala

    // Notificar a todos los usuarios en una sala específica
    Task NotifyRoomAsync<T>(string roomCode, string methodName, T data);
    Task NotifyRoomAsync(string roomCode, string methodName, object data);

    // Notificar a todos excepto al remitente
    Task NotifyRoomExceptAsync<T>(string roomCode, string excludeConnectionId, string methodName, T data);
    Task NotifyRoomExceptAsync(string roomCode, PlayerId excludePlayerId, string methodName, object data);

    #endregion

    #region Notificaciones específicas de jugador

    // Notificar a un jugador específico (todas sus conexiones)
    Task NotifyPlayerAsync<T>(PlayerId playerId, string methodName, T data);
    Task NotifyPlayerAsync(PlayerId playerId, string methodName, object data);

    // Notificar a una conexión específica
    Task NotifyConnectionAsync<T>(string connectionId, string methodName, T data);
    Task NotifyConnectionAsync(string connectionId, string methodName, object data);

    #endregion

    #region Notificaciones grupales

    // Notificar a todos los usuarios conectados (broadcast global)
    Task NotifyAllAsync<T>(string methodName, T data);
    Task NotifyAllAsync(string methodName, object data);

    // Notificar a un grupo específico
    Task NotifyGroupAsync<T>(string groupName, string methodName, T data);
    Task NotifyGroupAsync(string groupName, string methodName, object data);

    #endregion

    #region Eventos específicos del juego

    // Eventos de sala
    Task NotifyPlayerJoinedAsync(string roomCode, PlayerJoinedEventModel eventData);
    Task NotifyPlayerLeftAsync(string roomCode, PlayerLeftEventModel eventData);
    Task NotifySpectatorJoinedAsync(string roomCode, SpectatorModel spectator);
    Task NotifySpectatorLeftAsync(string roomCode, SpectatorModel spectator);

    // Eventos de juego
    Task NotifyGameStartedAsync(string roomCode, GameStartedEventModel eventData);
    Task NotifyGameEndedAsync(string roomCode, GameEndedEventModel eventData);
    Task NotifyTurnChangedAsync(string roomCode, TurnChangedEventModel eventData);
    Task NotifyCardDealtAsync(string roomCode, CardDealtEventModel eventData);
    Task NotifyPlayerActionAsync(string roomCode, PlayerActionEventModel eventData);
    Task NotifyBetPlacedAsync(string roomCode, BetPlacedEventModel eventData);

    // Estados de juego
    Task NotifyGameStateUpdatedAsync(string roomCode, GameStateModel gameState);
    Task NotifyRoomInfoUpdatedAsync(string roomCode, RoomInfoModel roomInfo);

    #endregion

    #region Notificaciones de lobby

    // Actualizar lista de salas activas
    Task NotifyActiveRoomsUpdatedAsync(List<ActiveRoomModel> activeRooms);
    Task NotifyRoomCreatedAsync(ActiveRoomModel newRoom);
    Task NotifyRoomClosedAsync(string roomCode);

    #endregion

    #region Notificaciones de error y éxito

    // Enviar mensajes de error/éxito a conexiones específicas
    Task SendErrorToConnectionAsync(string connectionId, string message, string? code = null);
    Task SendSuccessToConnectionAsync(string connectionId, string message, object? data = null);

    // Enviar mensajes de error/éxito a jugadores específicos
    Task SendErrorToPlayerAsync(PlayerId playerId, string message, string? code = null);
    Task SendSuccessToPlayerAsync(PlayerId playerId, string message, object? data = null);

    #endregion

    #region Chat (opcional)

    // Enviar mensajes de chat
    Task SendChatMessageAsync(string roomCode, ChatMessageModel message);
    Task SendSystemMessageAsync(string roomCode, string message);

    #endregion
}