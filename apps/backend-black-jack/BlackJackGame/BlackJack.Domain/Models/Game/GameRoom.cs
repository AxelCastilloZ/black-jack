using BlackJack.Domain.Common;
using BlackJack.Domain.Enums;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Models.Game;

public class GameRoom : AggregateRoot
{
    private readonly List<RoomPlayer> _players = new();
    private readonly List<Spectator> _spectators = new();

    // EF Core constructor
    protected GameRoom() : base()
    {
        RoomCode = string.Empty;
        Name = string.Empty;
        HostPlayerId = PlayerId.New();
        Status = RoomStatus.WaitingForPlayers;
        MaxPlayers = 6;
        CurrentPlayerIndex = 0;
    }

    // Constructor principal
    public GameRoom(string name, PlayerId hostPlayerId, string? roomCode = null, Guid? id = null)
        : base(id ?? Guid.NewGuid())
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        HostPlayerId = hostPlayerId ?? throw new ArgumentNullException(nameof(hostPlayerId));
        RoomCode = roomCode ?? GenerateRoomCode();
        Status = RoomStatus.WaitingForPlayers;
        MaxPlayers = 6;
        CurrentPlayerIndex = 0;
        BlackjackTableId = null;
    }

    // Propiedades principales
    public string RoomCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public PlayerId HostPlayerId { get; private set; } = default!;
    public RoomStatus Status { get; private set; }
    public int MaxPlayers { get; private set; }
    public int CurrentPlayerIndex { get; private set; }
    public Guid? BlackjackTableId { get; set; }

    // Navegación
    public IReadOnlyList<RoomPlayer> Players => _players.AsReadOnly();
    public IReadOnlyList<Spectator> Spectators => _spectators.AsReadOnly();

    // Propiedades calculadas
    public int PlayerCount => _players.Count;
    public bool IsFull => PlayerCount >= MaxPlayers;
    public bool CanStart => PlayerCount >= 1 && Status == RoomStatus.WaitingForPlayers;
    public bool IsGameInProgress => Status == RoomStatus.InProgress;
    public RoomPlayer? CurrentPlayer => _players.Count > 0 && CurrentPlayerIndex < _players.Count
        ? _players[CurrentPlayerIndex] : null;

    // Factory method
    public static GameRoom Create(string name, PlayerId hostPlayerId, string? roomCode = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Room name cannot be null or empty", nameof(name));

        return new GameRoom(name, hostPlayerId, roomCode);
    }

    // Generación de código de sala
    private static string GenerateRoomCode()
    {
        var random = new Random();
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    // Manejo de jugadores
    public void AddPlayer(PlayerId playerId, string playerName, bool isViewer = false)
    {
        if (playerId == null)
            throw new ArgumentNullException(nameof(playerId));

        if (string.IsNullOrWhiteSpace(playerName))
            throw new ArgumentException("Player name cannot be null or empty", nameof(playerName));

        if (IsFull)
            throw new InvalidOperationException("Room is full");

        if (IsPlayerInRoom(playerId))
            throw new InvalidOperationException("Player is already in the room");

        if (Status != RoomStatus.WaitingForPlayers)
            throw new InvalidOperationException("Cannot add players - game is in progress");

        var roomPlayer = new RoomPlayer(playerId, playerName, _players.Count, isViewer);
        _players.Add(roomPlayer);

        // Disparar evento
        AddDomainEvent(new PlayerJoinedRoomEvent(RoomCode, playerId, playerName, roomPlayer.Position, _players.Count));

        UpdateTimestamp();
    }

    public void RemovePlayer(PlayerId playerId)
    {
        var player = _players.FirstOrDefault(p => p.PlayerId.Value == playerId.Value);
        if (player == null) return;

        var playerName = player.Name;
        _players.Remove(player);

        // Reordenar posiciones
        for (int i = 0; i < _players.Count; i++)
        {
            _players[i].UpdatePosition(i);
        }

        // Ajustar índice del turno actual
        if (CurrentPlayerIndex >= _players.Count && _players.Count > 0)
        {
            CurrentPlayerIndex = 0;
        }

        // Si era el host, transferir a otro jugador
        if (player.PlayerId.Value == HostPlayerId.Value && _players.Count > 0)
        {
            HostPlayerId = _players[0].PlayerId;
        }

        // Disparar evento
        AddDomainEvent(new PlayerLeftRoomEvent(RoomCode, playerId, playerName, _players.Count));

        UpdateTimestamp();
    }

    // Manejo de espectadores - CORREGIDO: Dos sobrecargas
    public void AddSpectator(PlayerId playerId, string spectatorName)
    {
        if (IsPlayerInRoom(playerId))
            throw new InvalidOperationException("Player is already playing in this room");

        if (_spectators.Any(s => s.PlayerId.Value == playerId.Value))
            return; // Ya es espectador

        var spectator = Spectator.Create(playerId, spectatorName);
        _spectators.Add(spectator);

        // Disparar evento
        AddDomainEvent(new SpectatorJoinedEvent(RoomCode, playerId, spectatorName));

        UpdateTimestamp();
    }

    // NUEVO: Sobrecarga que acepta objeto Spectator
    public void AddSpectator(Spectator spectator)
    {
        if (spectator == null)
            throw new ArgumentNullException(nameof(spectator));

        if (IsPlayerInRoom(spectator.PlayerId))
            throw new InvalidOperationException("Player is already playing in this room");

        if (_spectators.Any(s => s.PlayerId.Value == spectator.PlayerId.Value))
            return; // Ya es espectador

        _spectators.Add(spectator);

        // Disparar evento
        AddDomainEvent(new SpectatorJoinedEvent(RoomCode, spectator.PlayerId, spectator.Name));

        UpdateTimestamp();
    }

    public void RemoveSpectator(PlayerId playerId)
    {
        var spectator = _spectators.FirstOrDefault(s => s.PlayerId.Value == playerId.Value);
        if (spectator != null)
        {
            var spectatorName = spectator.Name;
            _spectators.Remove(spectator);

            // Disparar evento
            AddDomainEvent(new SpectatorLeftEvent(RoomCode, playerId, spectatorName));

            UpdateTimestamp();
        }
    }

    // NUEVO: Sobrecarga que acepta objeto Spectator
    public void RemoveSpectator(Spectator spectator)
    {
        if (spectator != null && _spectators.Remove(spectator))
        {
            // Disparar evento
            AddDomainEvent(new SpectatorLeftEvent(RoomCode, spectator.PlayerId, spectator.Name));

            UpdateTimestamp();
        }
    }

    // Sistema de turnos - CORREGIDO: Dos sobrecargas para StartGame
    public void StartGame()
    {
        if (!CanStart)
            throw new InvalidOperationException("Cannot start game - conditions not met");

        Status = RoomStatus.InProgress;
        CurrentPlayerIndex = 0;

        // Disparar evento
        var playerNames = GetPlayerNames();
        var firstPlayer = _players.Count > 0 ? _players[0].PlayerId : PlayerId.New();
        AddDomainEvent(new GameStartedEvent(RoomCode, BlackjackTableId ?? Guid.NewGuid(), playerNames, firstPlayer));

        UpdateTimestamp();
    }

    public void StartGame(Guid blackjackTableId)
    {
        if (!CanStart)
            throw new InvalidOperationException("Cannot start game - conditions not met");

        BlackjackTableId = blackjackTableId;
        Status = RoomStatus.InProgress;
        CurrentPlayerIndex = 0;

        // Disparar evento
        var playerNames = GetPlayerNames();
        var firstPlayer = _players.Count > 0 ? _players[0].PlayerId : PlayerId.New();
        AddDomainEvent(new GameStartedEvent(RoomCode, blackjackTableId, playerNames, firstPlayer));

        UpdateTimestamp();
    }

    public void NextTurn()
    {
        if (Status != RoomStatus.InProgress)
            throw new InvalidOperationException("Game is not in progress");

        if (_players.Count == 0)
            throw new InvalidOperationException("No players in the room");

        var previousPlayer = CurrentPlayer;
        CurrentPlayerIndex = (CurrentPlayerIndex + 1) % _players.Count;
        var currentPlayer = CurrentPlayer;

        // Disparar evento
        if (currentPlayer != null)
        {
            AddDomainEvent(new TurnChangedEvent(
                RoomCode,
                currentPlayer.PlayerId,
                currentPlayer.Name,
                previousPlayer?.PlayerId,
                CurrentPlayerIndex));
        }

        UpdateTimestamp();
    }

    public void SetCurrentPlayer(PlayerId playerId)
    {
        var playerIndex = _players.FindIndex(p => p.PlayerId.Value == playerId.Value);
        if (playerIndex == -1)
            throw new InvalidOperationException("Player not found in room");

        CurrentPlayerIndex = playerIndex;
        UpdateTimestamp();
    }

    // Cambios de estado
    public void SetStatus(RoomStatus status)
    {
        Status = status;
        UpdateTimestamp();
    }

    public void EndGame()
    {
        Status = RoomStatus.Finished;
        BlackjackTableId = null;
        UpdateTimestamp();
    }

    public void ResetForNewGame()
    {
        Status = RoomStatus.WaitingForPlayers;
        CurrentPlayerIndex = 0;
        BlackjackTableId = null;

        // Resetear estado de jugadores
        foreach (var player in _players)
        {
            player.ResetForNewGame();
        }

        UpdateTimestamp();
    }

    // Configuración
    public void SetMaxPlayers(int maxPlayers)
    {
        if (maxPlayers < 1 || maxPlayers > 6)
            throw new ArgumentException("Max players must be between 1 and 6", nameof(maxPlayers));

        if (maxPlayers < PlayerCount)
            throw new InvalidOperationException("Cannot set max players below current player count");

        MaxPlayers = maxPlayers;
        UpdateTimestamp();
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        Name = name;
        UpdateTimestamp();
    }

    // CORREGIDO: Métodos de validación que comparan por Value en lugar de por referencia
    public bool IsPlayerInRoom(PlayerId playerId)
    {
        return _players.Any(p => p.PlayerId.Value == playerId.Value);
    }

    public bool IsHost(PlayerId playerId)
    {
        return HostPlayerId.Value == playerId.Value;
    }

    public bool IsPlayerTurn(PlayerId playerId)
    {
        return CurrentPlayer?.PlayerId.Value == playerId.Value;
    }

    public RoomPlayer? GetPlayer(PlayerId playerId)
    {
        return _players.FirstOrDefault(p => p.PlayerId.Value == playerId.Value);
    }

    // Métodos de información
    public List<string> GetPlayerNames()
    {
        return _players.Select(p => p.Name).ToList();
    }

    public string GetRoomInfo()
    {
        return $"Room {RoomCode}: {Name} ({PlayerCount}/{MaxPlayers} players) - {Status}";
    }
}

// Enum para estado de sala
public enum RoomStatus
{
    WaitingForPlayers = 0,
    InProgress = 1,
    Finished = 2,
    Paused = 3
}