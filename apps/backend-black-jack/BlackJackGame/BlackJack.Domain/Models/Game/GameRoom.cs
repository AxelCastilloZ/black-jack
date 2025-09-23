using BlackJack.Domain.Common;
using BlackJack.Domain.Enums;
using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Betting;

namespace BlackJack.Domain.Models.Game;

public class GameRoom : AggregateRoot
{

    protected GameRoom() : base()
    {
        RoomCode = string.Empty;
        Name = string.Empty;
        HostPlayerId = PlayerId.New();
        Status = RoomStatus.WaitingForPlayers;
        MaxPlayers = 6;
        CurrentPlayerIndex = 0;
        MinBetPerRound = new Money(10m);
        Players = new List<RoomPlayer>();
        Spectators = new List<Spectator>();
    }

    // Constructor principal
    public GameRoom(string name, PlayerId hostPlayerId, string? roomCode = null, decimal minBetPerRound = 10m, Guid? id = null)
        : base(id ?? Guid.NewGuid())
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        HostPlayerId = hostPlayerId ?? throw new ArgumentNullException(nameof(hostPlayerId));
        RoomCode = roomCode ?? GenerateRoomCode();
        Status = RoomStatus.WaitingForPlayers;
        MaxPlayers = 6;
        CurrentPlayerIndex = 0;
        BlackjackTableId = null;
        MinBetPerRound = new Money(minBetPerRound);
        Players = new List<RoomPlayer>();
        Spectators = new List<Spectator>();
    }

    // Propiedades principales
    public string RoomCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public PlayerId HostPlayerId { get; private set; } = default!;
    public RoomStatus Status { get; private set; }
    public int MaxPlayers { get; private set; }
    public int CurrentPlayerIndex { get; private set; }
    public Guid? BlackjackTableId { get; set; }

    // NUEVO: Sistema de apuestas automáticas
    public Money MinBetPerRound { get; private set; } = default!;

    // FIX DEFINITIVO: Collections que Entity Framework puede manejar correctamente
    // Setter privado para encapsulación, pero EF puede inicializar y populate
    public virtual ICollection<RoomPlayer> Players { get; private set; } = new List<RoomPlayer>();
    public virtual ICollection<Spectator> Spectators { get; private set; } = new List<Spectator>();

    // Métodos de acceso read-only para el dominio - MANTENIDOS para compatibilidad
    public IReadOnlyList<RoomPlayer> GetPlayers() => Players.ToList().AsReadOnly();
    public IReadOnlyList<Spectator> GetSpectators() => Spectators.ToList().AsReadOnly();

    // Propiedades calculadas
    public int PlayerCount => Players.Count;
    public bool IsFull => PlayerCount >= MaxPlayers;
    public bool CanStart => true; // Force-enabled for gameplay testing
    public bool IsGameInProgress => Status == RoomStatus.InProgress;

    // ✅ FIX CRÍTICO: CurrentPlayer ahora solo considera jugadores SENTADOS
    public RoomPlayer? CurrentPlayer
    {
        get
        {
            var seatedPlayers = Players.Where(p => p.IsSeated).OrderBy(p => p.SeatPosition).ToList();
            if (seatedPlayers.Count == 0 || CurrentPlayerIndex < 0 || CurrentPlayerIndex >= seatedPlayers.Count)
                return null;

            return seatedPlayers[CurrentPlayerIndex];
        }
    }

    // NUEVO: Propiedades calculadas para apuestas
    public int SeatedPlayersCount => Players.Count(p => p.IsSeated);
    public Money TotalBetPerRound => new Money(MinBetPerRound.Amount * SeatedPlayersCount);
    public IReadOnlyList<RoomPlayer> SeatedPlayers => Players.Where(p => p.IsSeated).OrderBy(p => p.SeatPosition).ToList().AsReadOnly();

    // Factory method
    public static GameRoom Create(string name, PlayerId hostPlayerId, string? roomCode = null, decimal minBetPerRound = 10m)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Room name cannot be null or empty", nameof(name));

        if (minBetPerRound <= 0)
            throw new ArgumentException("Minimum bet per round must be greater than zero", nameof(minBetPerRound));

        return new GameRoom(name, hostPlayerId, roomCode, minBetPerRound);
    }

    // Generación de código de sala
    private static string GenerateRoomCode()
    {
        var random = new Random();
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    // NUEVO: Configuración de apuestas
    public void SetMinBetPerRound(Money minBet)
    {
        if (minBet == null)
            throw new ArgumentNullException(nameof(minBet));

        if (minBet.Amount <= 0)
            throw new ArgumentException("Minimum bet must be greater than zero", nameof(minBet));

        if (Status == RoomStatus.InProgress)
            throw new InvalidOperationException("Cannot change bet amount during game");

        MinBetPerRound = minBet;
        UpdateTimestamp();
    }

    public void SetMinBetPerRound(decimal amount)
    {
        SetMinBetPerRound(new Money(amount));
    }

    // NUEVO: Validación de fondos para apuestas automáticas
    public bool CanPlayerAffordAutoBet(PlayerId playerId, Money playerBalance)
    {
        if (playerBalance == null) return false;
        return playerBalance.Amount >= MinBetPerRound.Amount;
    }

    // NUEVO: Obtener información de apuesta para mostrar en lobby
    public string GetBettingInfo()
    {
        return $"Apuesta automática por ronda: {MinBetPerRound}";
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

        var roomPlayer = new RoomPlayer(playerId, playerName, Players.Count, isViewer);
        Players.Add(roomPlayer);

        // Disparar evento
        AddDomainEvent(new PlayerJoinedRoomEvent(RoomCode, playerId, playerName, roomPlayer.Position, Players.Count));

        UpdateTimestamp();
    }

    public void RemovePlayer(PlayerId playerId)
    {
        var player = Players.FirstOrDefault(p => p.PlayerId.Value == playerId.Value);
        if (player == null) return;

        var playerName = player.Name;
        var wasSeated = player.IsSeated;
        Players.Remove(player);

        // Reordenar posiciones
        var playersList = Players.ToList();
        for (int i = 0; i < playersList.Count; i++)
        {
            playersList[i].UpdatePosition(i);
        }

        // ✅ FIX CRÍTICO: Ajustar índice del turno actual considerando solo jugadores SENTADOS
        if (wasSeated)
        {
            var seatedPlayers = Players.Where(p => p.IsSeated).OrderBy(p => p.SeatPosition).ToList();
            if (CurrentPlayerIndex >= seatedPlayers.Count && seatedPlayers.Count > 0)
            {
                CurrentPlayerIndex = 0;
            }
        }

        // Si era el host, transferir a otro jugador
        if (player.PlayerId.Value == HostPlayerId.Value && Players.Count > 0)
        {
            HostPlayerId = Players.First().PlayerId;
        }

        // Disparar evento
        AddDomainEvent(new PlayerLeftRoomEvent(RoomCode, playerId, playerName, Players.Count));

        UpdateTimestamp();
    }

    // Manejo de espectadores - CORREGIDO: Dos sobrecargas
    public void AddSpectator(PlayerId playerId, string spectatorName)
    {
        if (IsPlayerInRoom(playerId))
            throw new InvalidOperationException("Player is already playing in this room");

        if (Spectators.Any(s => s.PlayerId.Value == playerId.Value))
            return; // Ya es espectador

        var spectator = Spectator.Create(playerId, spectatorName);
        Spectators.Add(spectator);

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

        if (Spectators.Any(s => s.PlayerId.Value == spectator.PlayerId.Value))
            return; // Ya es espectador

        Spectators.Add(spectator);

        // Disparar evento
        AddDomainEvent(new SpectatorJoinedEvent(RoomCode, spectator.PlayerId, spectator.Name));

        UpdateTimestamp();
    }

    public void RemoveSpectator(PlayerId playerId)
    {
        var spectator = Spectators.FirstOrDefault(s => s.PlayerId.Value == playerId.Value);
        if (spectator != null)
        {
            var spectatorName = spectator.Name;
            Spectators.Remove(spectator);

            // Disparar evento
            AddDomainEvent(new SpectatorLeftEvent(RoomCode, playerId, spectatorName));

            UpdateTimestamp();
        }
    }

    // NUEVO: Sobrecarga que acepta objeto Spectator
    public void RemoveSpectator(Spectator spectator)
    {
        if (spectator != null && Spectators.Remove(spectator))
        {
            // Disparar evento
            AddDomainEvent(new SpectatorLeftEvent(RoomCode, spectator.PlayerId, spectator.Name));

            UpdateTimestamp();
        }
    }

    // ✅ FIX CRÍTICO: Sistema de turnos - CORREGIDO para usar solo jugadores sentados
    public void StartGame()
    {
        if (!CanStart)
            throw new InvalidOperationException("Cannot start game - conditions not met");

        Status = RoomStatus.InProgress;

        // ✅ CAMBIO CRÍTICO: Empezar con el primer jugador SENTADO
        var seatedPlayers = Players.Where(p => p.IsSeated).OrderBy(p => p.SeatPosition).ToList();
        CurrentPlayerIndex = seatedPlayers.Count > 0 ? 0 : -1;

        // Disparar evento
        var playerNames = GetPlayerNames();
        var firstPlayer = CurrentPlayer?.PlayerId ?? PlayerId.New();
        AddDomainEvent(new GameStartedEvent(RoomCode, BlackjackTableId ?? Guid.NewGuid(), playerNames, firstPlayer));

        UpdateTimestamp();
    }

    public void StartGame(Guid blackjackTableId)
    {
        if (!CanStart)
            throw new InvalidOperationException("Cannot start game - conditions not met");

        BlackjackTableId = blackjackTableId;
        Status = RoomStatus.InProgress;

        // ✅ CAMBIO CRÍTICO: Empezar con el primer jugador SENTADO
        var seatedPlayers = Players.Where(p => p.IsSeated).OrderBy(p => p.SeatPosition).ToList();
        CurrentPlayerIndex = seatedPlayers.Count > 0 ? 0 : -1;

        // Disparar evento
        var playerNames = GetPlayerNames();
        var firstPlayer = CurrentPlayer?.PlayerId ?? PlayerId.New();
        AddDomainEvent(new GameStartedEvent(RoomCode, blackjackTableId, playerNames, firstPlayer));

        UpdateTimestamp();
    }

    // ✅ FIX CRÍTICO: NextTurn() ahora avanza solo entre jugadores SENTADOS
    public void NextTurn()
    {
        if (Status != RoomStatus.InProgress)
            throw new InvalidOperationException("Game is not in progress");

        var seatedPlayers = Players.Where(p => p.IsSeated).OrderBy(p => p.SeatPosition).ToList();
        if (seatedPlayers.Count == 0)
            throw new InvalidOperationException("No seated players in the room");

        var previousPlayer = CurrentPlayer;
        CurrentPlayerIndex = (CurrentPlayerIndex + 1) % seatedPlayers.Count;
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

    // ✅ FIX CRÍTICO: SetCurrentPlayer() ahora valida que el jugador esté sentado
    public void SetCurrentPlayer(PlayerId playerId)
    {
        var seatedPlayers = Players.Where(p => p.IsSeated).OrderBy(p => p.SeatPosition).ToList();
        var playerIndex = seatedPlayers.FindIndex(p => p.PlayerId.Value == playerId.Value);

        if (playerIndex == -1)
            throw new InvalidOperationException("Player not found in seated players or player is not seated");

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
        foreach (var player in Players)
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
        return Players.Any(p => p.PlayerId.Value == playerId.Value);
    }

    public bool IsHost(PlayerId playerId)
    {
        return HostPlayerId.Value == playerId.Value;
    }

    // ✅ FIX CRÍTICO: IsPlayerTurn() ahora funciona correctamente con el nuevo CurrentPlayer
    public bool IsPlayerTurn(PlayerId playerId)
    {
        return CurrentPlayer?.PlayerId.Value == playerId.Value;
    }

    public RoomPlayer? GetPlayer(PlayerId playerId)
    {
        return Players.FirstOrDefault(p => p.PlayerId.Value == playerId.Value);
    }

    // ✅ MÉTODO CORREGIDO: Establecer el primer jugador sentado como actual
    public void SetFirstSeatedPlayerAsCurrent()
    {
        var seatedPlayers = Players.Where(p => p.IsSeated).OrderBy(p => p.SeatPosition).ToList();
        if (seatedPlayers.Count > 0)
        {
            CurrentPlayerIndex = 0; // Siempre el primer jugador sentado (índice 0 en lista de sentados)
            UpdateTimestamp();
        }
    }

    // ✅ NUEVO: Verificar si un jugador está sentado
    public bool IsPlayerSeated(PlayerId playerId)
    {
        var player = GetPlayer(playerId);
        return player?.IsSeated ?? false;
    }

    // ✅ NUEVO: Obtener jugador sentado por posición de asiento
    public RoomPlayer? GetPlayerBySeat(int seatPosition)
    {
        return Players.FirstOrDefault(p => p.SeatPosition == seatPosition);
    }

    // ✅ NUEVO: Obtener nombres de jugadores sentados solamente
    public List<string> GetSeatedPlayerNames()
    {
        return Players.Where(p => p.IsSeated)
                     .OrderBy(p => p.SeatPosition)
                     .Select(p => p.Name)
                     .ToList();
    }

    // ✅ NUEVO: Debug info para troubleshooting
    public string GetTurnInfo()
    {
        var seatedPlayers = Players.Where(p => p.IsSeated).OrderBy(p => p.SeatPosition).ToList();
        var currentPlayerName = CurrentPlayer?.Name ?? "None";
        return $"Current turn: {currentPlayerName} (Index: {CurrentPlayerIndex}/{seatedPlayers.Count - 1})";
    }

    // Métodos de información
    public List<string> GetPlayerNames()
    {
        return Players.Select(p => p.Name).ToList();
    }

    public string GetRoomInfo()
    {
        return $"Room {RoomCode}: {Name} ({PlayerCount}/{MaxPlayers} players, {SeatedPlayersCount} seated) - {Status} - {GetBettingInfo()}";
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