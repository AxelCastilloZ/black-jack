// BlackJack.Realtime/Models/HubMethodNames.cs - Constantes organizadas por hub
using BlackJack.Realtime.Hubs;

namespace BlackJack.Realtime.Models;

public static class HubMethodNames
{
    #region Métodos del servidor (Server -> Client)

    public static class ServerMethods
    {
        // === Respuestas generales ===
        public const string Success = "Success";
        public const string Error = "Error";
        public const string TestResponse = "TestResponse";

        // === GameRoomHub - Gestión de salas ===
        public const string RoomCreated = "RoomCreated";
        public const string RoomJoined = "RoomJoined";
        public const string RoomLeft = "RoomLeft";
        public const string RoomInfo = "RoomInfo";
        public const string RoomInfoUpdated = "RoomInfoUpdated";

        // === GameRoomHub - Gestión de asientos ===
        public const string SeatJoined = "SeatJoined";
        public const string SeatLeft = "SeatLeft";

        // === GameRoomHub - Eventos de jugadores ===
        public const string PlayerJoined = "PlayerJoined";
        public const string PlayerLeft = "PlayerLeft";
        public const string SpectatorJoined = "SpectatorJoined";
        public const string SpectatorLeft = "SpectatorLeft";

        // === GameControlHub - Control de juego ===
        public const string GameStarted = "GameStarted";
        public const string GameEnded = "GameEnded";
        public const string GameStateUpdated = "GameStateUpdated";
        public const string TurnChanged = "TurnChanged";

        // === GameControlHub - Acciones de cartas ===
        public const string CardDealt = "CardDealt";
        public const string PlayerActionPerformed = "PlayerActionPerformed";
        public const string BetPlaced = "BetPlaced";

        // === GameControlHub - Auto-Betting (Eventos grupales) ===
        public const string AutoBetProcessed = "AutoBetProcessed";
        public const string AutoBetProcessingStarted = "AutoBetProcessingStarted";
        public const string AutoBetStatistics = "AutoBetStatistics";
        public const string AutoBetFailed = "AutoBetFailed";
        public const string AutoBetRoundSummary = "AutoBetRoundSummary";

        // === GameControlHub - Auto-Betting (Eventos de jugadores) ===
        public const string PlayerRemovedFromSeat = "PlayerRemovedFromSeat";
        public const string PlayerBalanceUpdated = "PlayerBalanceUpdated";
        public const string InsufficientFundsWarning = "InsufficientFundsWarning";
        public const string MinBetPerRoundUpdated = "MinBetPerRoundUpdated";

        // === GameControlHub - Auto-Betting (Notificaciones personales) ===
        public const string YouWereRemovedFromSeat = "YouWereRemovedFromSeat";
        public const string YourBalanceUpdated = "YourBalanceUpdated";
        public const string InsufficientFundsWarningPersonal = "InsufficientFundsWarningPersonal";
        public const string AutoBetFailedPersonal = "AutoBetFailedPersonal";

        // === LobbyHub - Gestión del lobby ===
        public const string ActiveRoomsUpdated = "ActiveRoomsUpdated";
        public const string RoomListUpdated = "RoomListUpdated";

        // === Chat (opcional) ===
        public const string MessageReceived = "MessageReceived";
    }

    #endregion

    #region Métodos del cliente (Client -> Server)

    public static class ClientMethods
    {
        // === BaseHub ===
        public const string TestConnection = "TestConnection";

        // === GameRoomHub - Gestión de salas ===
        public const string CreateRoom = "CreateRoom";
        public const string JoinRoom = "JoinRoom";
        public const string JoinOrCreateRoomForTable = "JoinOrCreateRoomForTable";
        public const string LeaveRoom = "LeaveRoom";
        public const string GetRoomInfo = "GetRoomInfo";

        // === GameRoomHub - Gestión de asientos ===
        public const string JoinSeat = "JoinSeat";
        public const string LeaveSeat = "LeaveSeat";

        // === GameRoomHub - Espectadores ===
        public const string JoinAsViewer = "JoinAsViewer";

        // === GameControlHub - Control de juego ===
        public const string StartGame = "StartGame";
        public const string EndGame = "EndGame";
        public const string JoinRoomForGameControl = "JoinRoomForGameControl";
        public const string LeaveRoomGameControl = "LeaveRoomGameControl";

        // === GameControlHub - Acciones de jugador ===
        public const string Hit = "Hit";
        public const string Stand = "Stand";
        public const string DoubleDown = "DoubleDown";
        public const string Split = "Split";

        // === GameControlHub - Auto-Betting ===
        public const string ProcessRoundAutoBets = "ProcessRoundAutoBets";
        public const string GetAutoBetStatistics = "GetAutoBetStatistics";

        // === LobbyHub ===
        public const string JoinLobby = "JoinLobby";
        public const string LeaveLobby = "LeaveLobby";
        public const string GetActiveRooms = "GetActiveRooms";
        public const string RefreshRooms = "RefreshRooms";
        public const string QuickJoin = "QuickJoin";
        public const string QuickJoinTable = "QuickJoinTable";
        public const string GetLobbyStats = "GetLobbyStats";
        public const string GetRoomDetails = "GetRoomDetails";
    }

    #endregion

    #region Grupos de SignalR

    public static class Groups
    {
        // Grupo principal del lobby
        public const string LobbyGroup = "Lobby";

        // Generadores de nombres de grupos
        public static string GetRoomGroup(string roomCode) => $"Room_{roomCode}";
        public static string GetTableGroup(string tableId) => $"Table_{tableId}";

        // Métodos de utilidad para validar grupos
        public static bool IsRoomGroup(string groupName) => groupName.StartsWith("Room_");
        public static bool IsTableGroup(string groupName) => groupName.StartsWith("Table_");
        public static bool IsLobbyGroup(string groupName) => groupName == LobbyGroup;

        // Extraer IDs de nombres de grupos
        public static string? ExtractRoomCodeFromGroup(string groupName)
        {
            return IsRoomGroup(groupName) ? groupName.Substring(5) : null;
        }

        public static string? ExtractTableIdFromGroup(string groupName)
        {
            return IsTableGroup(groupName) ? groupName.Substring(6) : null;
        }
    }

    #endregion

    #region Categorías de métodos por funcionalidad

    public static class Categories
    {
        // Métodos relacionados con la gestión de salas
        public static readonly string[] RoomManagement = new[]
        {
            ServerMethods.RoomCreated,
            ServerMethods.RoomJoined,
            ServerMethods.RoomLeft,
            ServerMethods.RoomInfo,
            ServerMethods.RoomInfoUpdated,
            ClientMethods.CreateRoom,
            ClientMethods.JoinRoom,
            ClientMethods.LeaveRoom,
            ClientMethods.GetRoomInfo
        };

        // Métodos relacionados con asientos
        public static readonly string[] SeatManagement = new[]
        {
            ServerMethods.SeatJoined,
            ServerMethods.SeatLeft,
            ClientMethods.JoinSeat,
            ClientMethods.LeaveSeat
        };

        // Métodos relacionados con el control de juego
        public static readonly string[] GameControl = new[]
        {
            ServerMethods.GameStarted,
            ServerMethods.GameEnded,
            ServerMethods.GameStateUpdated,
            ServerMethods.TurnChanged,
            ClientMethods.StartGame,
            ClientMethods.EndGame,
            ClientMethods.Hit,
            ClientMethods.Stand,
            ClientMethods.DoubleDown,
            ClientMethods.Split
        };

        // Métodos relacionados con auto-betting
        public static readonly string[] AutoBetting = new[]
        {
            ServerMethods.AutoBetProcessed,
            ServerMethods.AutoBetProcessingStarted,
            ServerMethods.AutoBetStatistics,
            ServerMethods.AutoBetFailed,
            ServerMethods.PlayerBalanceUpdated,
            ServerMethods.InsufficientFundsWarning,
            ClientMethods.ProcessRoundAutoBets,
            ClientMethods.GetAutoBetStatistics
        };

        // Métodos relacionados con el lobby
        public static readonly string[] Lobby = new[]
        {
            ServerMethods.ActiveRoomsUpdated,
            ServerMethods.RoomListUpdated,
            ClientMethods.JoinLobby,
            ClientMethods.LeaveLobby,
            ClientMethods.GetActiveRooms,
            ClientMethods.QuickJoin,
            ClientMethods.GetLobbyStats
        };

        // Métodos de utilidad para verificar categorías
        public static bool IsRoomManagementMethod(string methodName) =>
            RoomManagement.Contains(methodName);

        public static bool IsGameControlMethod(string methodName) =>
            GameControl.Contains(methodName);

        public static bool IsAutoBettingMethod(string methodName) =>
            AutoBetting.Contains(methodName);

        public static bool IsLobbyMethod(string methodName) =>
            Lobby.Contains(methodName);
    }

    #endregion

    #region Métodos de utilidad

    /// <summary>
    /// Determina qué hub debe manejar un método específico
    /// </summary>
    public static string GetResponsibleHub(string methodName)
    {
        if (Categories.IsRoomManagementMethod(methodName) || Categories.SeatManagement.Contains(methodName))
            return nameof(GameRoomHub);

        if (Categories.IsGameControlMethod(methodName) || Categories.IsAutoBettingMethod(methodName))
            return nameof(GameControlHub);

        if (Categories.IsLobbyMethod(methodName))
            return nameof(LobbyHub);

        return "Unknown";
    }

    /// <summary>
    /// Verifica si un método es un evento del servidor
    /// </summary>
    public static bool IsServerMethod(string methodName)
    {
        return typeof(ServerMethods).GetFields()
            .Any(field => field.GetValue(null)?.ToString() == methodName);
    }

    /// <summary>
    /// Verifica si un método es un comando del cliente
    /// </summary>
    public static bool IsClientMethod(string methodName)
    {
        return typeof(ClientMethods).GetFields()
            .Any(field => field.GetValue(null)?.ToString() == methodName);
    }

    #endregion
}