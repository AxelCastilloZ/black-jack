// HubMethodNames.cs - En BlackJack.Realtime/Models/
namespace BlackJack.Realtime.Models;

public static class HubMethodNames
{
    #region Métodos llamados DESDE el cliente (Client -> Server)

    // GameHub - Métodos que el cliente puede invocar
    public static class ClientMethods
    {
        // Gestión de salas
        public const string JoinRoom = "JoinRoom";
        public const string LeaveRoom = "LeaveRoom";
        public const string CreateRoom = "CreateRoom";
        public const string GetRoomInfo = "GetRoomInfo";

        // Control de juego
        public const string StartGame = "StartGame";
        public const string PlayerAction = "PlayerAction";
        public const string PlaceBet = "PlaceBet";

        // Chat (opcional)
        public const string SendMessage = "SendMessage";

        // Lobby
        public const string GetActiveRooms = "GetActiveRooms";
        public const string JoinLobby = "JoinLobby";
        public const string LeaveLobby = "LeaveLobby";
    }

    #endregion

    #region Métodos enviados AL cliente (Server -> Client)

    // Métodos que el servidor envía al cliente
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

        // Eventos de jugadores
        public const string PlayerJoined = "PlayerJoined";
        public const string PlayerLeft = "PlayerLeft";
        public const string SpectatorJoined = "SpectatorJoined";
        public const string SpectatorLeft = "SpectatorLeft";

        // Eventos de juego
        public const string GameStarted = "GameStarted";
        public const string GameEnded = "GameEnded";
        public const string TurnChanged = "TurnChanged";
        public const string CardDealt = "CardDealt";
        public const string PlayerActionPerformed = "PlayerActionPerformed";
        public const string BetPlaced = "BetPlaced";

        // Estados de juego
        public const string GameStateUpdated = "GameStateUpdated";
        public const string HandUpdated = "HandUpdated";
        public const string DealerHandUpdated = "DealerHandUpdated";

        // Chat
        public const string MessageReceived = "MessageReceived";

        // Lobby
        public const string ActiveRoomsUpdated = "ActiveRoomsUpdated";
        public const string RoomListUpdated = "RoomListUpdated";

        // Conexión
        public const string ConnectionEstablished = "ConnectionEstablished";
        public const string Reconnected = "Reconnected";
    }

    #endregion

    #region Nombres de grupos de SignalR

    public static class Groups
    {
        public const string LobbyGroup = "Lobby";

        // Formato para grupos de sala: "Room_ABC123"
        public static string GetRoomGroup(string roomCode) => $"Room_{roomCode}";

        // Formato para grupos de usuario: "User_12345678-1234-1234-1234-123456789012"
        public static string GetUserGroup(Guid playerId) => $"User_{playerId}";

        // Formato para grupos de tabla: "Table_12345678-1234-1234-1234-123456789012"
        public static string GetTableGroup(Guid tableId) => $"Table_{tableId}";
    }

    #endregion

    #region Parámetros comunes

    public static class Parameters
    {
        public const string RoomCode = "roomCode";
        public const string PlayerName = "playerName";
        public const string Message = "message";
        public const string Action = "action";
        public const string BetAmount = "betAmount";
        public const string RoomName = "roomName";
    }

    #endregion
}