using BlackJack.Domain.Common;
using BlackJack.Domain.Enums;
using BlackJack.Domain.Models.Betting;
using BlackJack.Domain.Models.Cards;
using BlackJack.Domain.Models.Users;

namespace BlackJack.Domain.Models.Game;

public class BlackjackTable : AggregateRoot
{
    private readonly List<Seat> _seats = new();
    private readonly List<Spectator> _spectators = new();

    // EF Core constructor
    protected BlackjackTable() : base()
    {
        Name = string.Empty;
        MinBet = new Money(10m);
        MaxBet = new Money(500m);
        Status = GameStatus.WaitingForPlayers;
        Deck = Deck.CreateShuffled();
        DealerHandId = null;
        RoundNumber = 0;
        InitializeSeats();
    }

    // Constructor principal
    public BlackjackTable(string name, Guid? id = null) : base(id ?? Guid.NewGuid())
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        MinBet = new Money(10m);
        MaxBet = new Money(500m);
        Status = GameStatus.WaitingForPlayers;
        Deck = Deck.CreateShuffled();
        DealerHandId = null;
        RoundNumber = 0;
        InitializeSeats();
    }

    // Propiedades principales
    public string Name { get; private set; } = string.Empty;
    public Money MinBet { get; private set; } = new Money(10m);
    public Money MaxBet { get; private set; } = new Money(500m);
    public GameStatus Status { get; private set; }
    public Deck Deck { get; private set; } = default!;
    public Guid? DealerHandId { get; private set; }
    public int RoundNumber { get; private set; }

    // Navegación para EF Core
    public IReadOnlyList<Seat> Seats => _seats.AsReadOnly();
    public IReadOnlyList<Spectator> Spectators => _spectators.AsReadOnly();

    // Propiedades calculadas
    public int SeatedPlayerCount => _seats.Count(s => s.IsOccupied);
    public bool HasDealerHand => DealerHandId.HasValue;
    public bool CanStartRound => Status == GameStatus.WaitingForPlayers &&
                                SeatedPlayerCount >= 1; // Bets disabled for gameplay testing

    // Inicialización
    private void InitializeSeats()
    {
        _seats.Clear();
        for (int i = 0; i < 6; i++)
        {
            _seats.Add(Seat.Create(i));
        }
    }

    // Factory method
    public static BlackjackTable Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Table name cannot be null or empty", nameof(name));

        return new BlackjackTable(name);
    }

    // Configuración de mesa
    public void SetBetLimits(Money min, Money max)
    {
        if (min == null || max == null)
            throw new ArgumentNullException("Bet limits cannot be null");

        if (min.Amount <= 0 || max.Amount <= 0)
            throw new ArgumentException("Bet limits must be greater than zero");

        if (min.Amount > max.Amount)
            throw new ArgumentException("MinBet cannot be greater than MaxBet");

        MinBet = min;
        MaxBet = max;
        UpdateTimestamp();
    }

    // Manejo de rondas
    public void StartNewRound()
    {
        // TEMP: Do not block starting round during gameplay testing
        // if (!CanStartRound)
        //     throw new InvalidOperationException("Cannot start round: conditions not met");

        if (!CanStartRound)
        {
            // Normalize minimal state: at least mark waiting and proceed
            Status = GameStatus.WaitingForPlayers;
        }

        RoundNumber++;
        Status = GameStatus.InProgress;

        // Crear nueva mano para el dealer
        DealerHandId = Guid.NewGuid();

        // Reset player hands - los HandIds se limpiarán en los players
        foreach (var seat in _seats.Where(s => s.IsOccupied))
        {
            seat.Player!.ResetForNewRound();
            // Se agregará una nueva mano cuando se reparten cartas
        }

        UpdateTimestamp();
    }

    // TEMP: Force start without validation for gameplay testing
    public void ForceStartRound()
    {
        RoundNumber++;
        Status = GameStatus.InProgress;

        // Create dealer hand id
        DealerHandId = Guid.NewGuid();

        // Reset player hands
        foreach (var seat in _seats.Where(s => s.IsOccupied))
        {
            seat.Player!.ResetForNewRound();
        }

        UpdateTimestamp();
    }

    public void EndRound()
    {
        Status = GameStatus.RoundEnded;
        UpdateTimestamp();

        // Barajar si quedan pocas cartas
        if (Deck.ShouldShuffle())
        {
            ResetDeck();
        }
    }

    public void SetWaitingForPlayers()
    {
        Status = GameStatus.WaitingForPlayers;
        UpdateTimestamp();
    }

    public void SetDealerHandId(Guid dealerHandId)
    {
        DealerHandId = dealerHandId;
        UpdateTimestamp();
    }

    // Manejo del deck
    public Card DealCard()
    {
        if (Deck.IsEmpty)
            throw new InvalidOperationException("Cannot deal from empty deck");

        var card = Deck.DealCard();
        UpdateTimestamp();
        return card;
    }

    public void ResetDeck()
    {
        Deck = Deck.CreateShuffled();
        UpdateTimestamp();
    }

    // Manejo de jugadores
    public void SeatPlayer(Player player, int position)
    {
        if (player == null)
            throw new ArgumentNullException(nameof(player));

        if (position < 0 || position >= _seats.Count)
            throw new ArgumentException("Invalid seat position", nameof(position));

        var seat = _seats[position];
        if (seat.IsOccupied)
            throw new InvalidOperationException("Seat is already occupied");

        // Verificar que el jugador no esté ya sentado en otra posición
        if (IsPlayerSeated(player.PlayerId))
            throw new InvalidOperationException("Player is already seated at this table");

        seat.SeatPlayer(player);
        player.SetActive(true);
        UpdateTimestamp();
    }

    public void RemovePlayer(PlayerId playerId)
    {
        if (playerId == null)
            throw new ArgumentNullException(nameof(playerId));

        var seat = GetPlayerSeat(playerId);
        if (seat != null)
        {
            seat.Player!.SetActive(false);
            seat.ClearSeat();
            UpdateTimestamp();
        }
    }

    public Seat? GetPlayerSeat(PlayerId playerId)
    {
        return _seats.FirstOrDefault(s => s.IsOccupied && s.Player!.PlayerId == playerId);
    }

    public bool IsPlayerSeated(PlayerId playerId)
    {
        return GetPlayerSeat(playerId) != null;
    }

    // Manejo de espectadores
    public void AddSpectator(Spectator spectator)
    {
        if (spectator == null)
            throw new ArgumentNullException(nameof(spectator));

        if (_spectators.Any(s => s.PlayerId == spectator.PlayerId))
            return; // Ya existe

        _spectators.Add(spectator);
        UpdateTimestamp();
    }

    public void RemoveSpectator(PlayerId playerId)
    {
        if (playerId == null)
            throw new ArgumentNullException(nameof(playerId));

        var spectator = _spectators.FirstOrDefault(s => s.PlayerId == playerId);
        if (spectator != null)
        {
            _spectators.Remove(spectator);
            UpdateTimestamp();
        }
    }

    // Validaciones privadas
    private bool AllPlayersHaveBets()
    {
        return _seats.Where(s => s.IsOccupied)
                    .All(s => s.Player!.HasActiveBet);
    }

    // Métodos de utilidad
    public void Reset()
    {
        // Limpiar todos los asientos
        foreach (var seat in _seats)
        {
            if (seat.IsOccupied)
            {
                seat.Player!.SetActive(false);
                seat.ClearSeat();
            }
        }

        // Limpiar espectadores
        _spectators.Clear();

        // Resetear estado
        Status = GameStatus.WaitingForPlayers;
        RoundNumber = 0;
        DealerHandId = null;
        Deck = Deck.CreateShuffled();

        UpdateTimestamp();
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        Name = name;
        UpdateTimestamp();
    }

    // Métodos para el juego
    public void CreatePlayerHand(PlayerId playerId)
    {
        var seat = GetPlayerSeat(playerId);
        if (seat == null)
            throw new InvalidOperationException("Player is not seated at this table");

        var handId = Guid.NewGuid();
        seat.Player!.AddHandId(handId);
        UpdateTimestamp();
    }

    public List<Player> GetSeatedPlayers()
    {
        return _seats.Where(s => s.IsOccupied)
                    .Select(s => s.Player!)
                    .ToList();
    }

    public bool ValidateBetAmount(Money betAmount)
    {
        return betAmount != null &&
               betAmount.Amount >= MinBet.Amount &&
               betAmount.Amount <= MaxBet.Amount;
    }
}