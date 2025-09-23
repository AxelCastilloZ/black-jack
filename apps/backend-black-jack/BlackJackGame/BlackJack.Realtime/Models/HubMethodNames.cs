// BlackJack.Realtime/Models/HubMethodNames.cs - ARCHIVO COMPLETO CON AUTO-BETTING
namespace BlackJack.Realtime.Models;

public static class HubMethodNames
{
    public static class ServerMethods
    {
        // Respuestas generales
        public const string Success = "Success";
        public const string Error = "Error";

        // Estados de sala
        public const string RoomJoined = "RoomJoined";
        public const string RoomLeft = "RoomLeft";
        public const string RoomCreated = "RoomCreated";
        public const string RoomInfo = "RoomInfo";
        public const string RoomInfoUpdated = "RoomInfoUpdated";

        // Eventos de asientos
        public const string SeatJoined = "SeatJoined";
        public const string SeatLeft = "SeatLeft";

        // Eventos de jugadores
        public const string PlayerJoined = "PlayerJoined";
        public const string PlayerLeft = "PlayerLeft";
        public const string SpectatorJoined = "SpectatorJoined";
        public const string SpectatorLeft = "SpectatorLeft";

        // Eventos de juego
        public const string GameStarted = "GameStarted";
        public const string GameEnded = "GameEnded";
        public const string TurnChanged = "TurnChanged";
        public const string GameStateUpdated = "GameStateUpdated";
        public const string CardDealt = "CardDealt";
        public const string PlayerActionPerformed = "PlayerActionPerformed";
        public const string BetPlaced = "BetPlaced";

        // Auto-Betting Events - Eventos grupales (para toda la sala)
        public const string AutoBetProcessed = "AutoBetProcessed";
        public const string AutoBetProcessingStarted = "AutoBetProcessingStarted";
        public const string AutoBetStatistics = "AutoBetStatistics";
        public const string AutoBetFailed = "AutoBetFailed";
        public const string AutoBetRoundSummary = "AutoBetRoundSummary";
        public const string MinBetPerRoundUpdated = "MinBetPerRoundUpdated";

        // Auto-Betting Events - Eventos de jugadores específicos (para toda la sala)
        public const string PlayerRemovedFromSeat = "PlayerRemovedFromSeat";
        public const string PlayerBalanceUpdated = "PlayerBalanceUpdated";
        public const string InsufficientFundsWarning = "InsufficientFundsWarning";

        // Auto-Betting Events - Notificaciones personales (solo al jugador afectado)
        public const string YouWereRemovedFromSeat = "YouWereRemovedFromSeat";
        public const string YourBalanceUpdated = "YourBalanceUpdated";
        public const string InsufficientFundsWarningPersonal = "InsufficientFundsWarningPersonal";
        public const string AutoBetFailedPersonal = "AutoBetFailedPersonal";

        // Chat
        public const string MessageReceived = "MessageReceived";

        // Lobby
        public const string ActiveRoomsUpdated = "ActiveRoomsUpdated";
        public const string RoomListUpdated = "RoomListUpdated";

        // Test
        public const string TestResponse = "TestResponse";
    }

    public static class Groups
    {
        public const string LobbyGroup = "Lobby";

        public static string GetRoomGroup(string roomCode) => $"Room_{roomCode}";

        public static string GetTableGroup(string tableId) => $"Table_{tableId}";
    }
}