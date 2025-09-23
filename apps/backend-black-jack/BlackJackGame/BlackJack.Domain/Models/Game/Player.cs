using BlackJack.Domain.Common;
using BlackJack.Domain.Models.Betting;
using BlackJack.Domain.Models.Game;

namespace BlackJack.Domain.Models.Users;

public class Player : BaseEntity
{
    // Constructor sin parámetros para EF Core
    protected Player() : base()
    {
        PlayerId = PlayerId.New();
        Name = string.Empty;
        Balance = new Money(0m);
        HandIds = new List<Guid>();
    }

    // Constructor principal  
    public Player(PlayerId playerId, string name, Money balance, Guid? id = null)
        : base(id ?? Guid.NewGuid())
    {
        PlayerId = playerId ?? throw new ArgumentNullException(nameof(playerId));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Balance = balance ?? throw new ArgumentNullException(nameof(balance));
        CurrentBet = null;
        IsActive = false;
        HandIds = new List<Guid>();
    }

    // Propiedades principales
    public PlayerId PlayerId { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public Money Balance { get; private set; } = default!;
    public Bet? CurrentBet { get; private set; }
    public bool IsActive { get; private set; }

    // *** FIX CRÍTICO: HandIds ahora es una propiedad pública para Entity Framework ***
    public List<Guid> HandIds { get; private set; } = new();

    // Propiedades calculadas
    public bool HasActiveBet => CurrentBet != null;
    public bool CanAffordBet(Money amount) => Balance.Amount >= amount.Amount;
    public bool HasSufficientFunds(decimal amount) => Balance.Amount >= amount;

    // Métodos de apuesta
    public void PlaceBet(Bet bet)
    {
        if (bet == null)
            throw new ArgumentNullException(nameof(bet));

        if (bet.Amount.Amount > Balance.Amount)
            throw new InvalidOperationException("Fondos insuficientes para realizar la apuesta");

        if (HasActiveBet)
            throw new InvalidOperationException("Ya hay una apuesta activa");

        CurrentBet = bet;
        Balance = new Money(Balance.Amount - bet.Amount.Amount);
        UpdateTimestamp();
    }

    public void ClearBet()
    {
        CurrentBet = null;
        UpdateTimestamp();
    }

    // Métodos de manos - ahora trabajan directamente con la propiedad HandIds
    public void AddHandId(Guid handId)
    {
        if (!HandIds.Contains(handId))
        {
            HandIds.Add(handId);
            UpdateTimestamp();
        }
    }

    public void RemoveHandId(Guid handId)
    {
        if (HandIds.Remove(handId))
        {
            UpdateTimestamp();
        }
    }

    public void ClearHandIds()
    {
        HandIds.Clear();
        UpdateTimestamp();
    }

    // Métodos de balance
    public void AddToBalance(Money amount)
    {
        if (amount == null)
            throw new ArgumentNullException(nameof(amount));

        Balance = new Money(Balance.Amount + amount.Amount);
        UpdateTimestamp();
    }

    public void SubtractFromBalance(Money amount)
    {
        if (amount == null)
            throw new ArgumentNullException(nameof(amount));

        if (amount.Amount > Balance.Amount)
            throw new InvalidOperationException("Fondos insuficientes");

        Balance = new Money(Balance.Amount - amount.Amount);
        UpdateTimestamp();
    }

    public void SetBalance(Money balance)
    {
        Balance = balance ?? throw new ArgumentNullException(nameof(balance));
        UpdateTimestamp();
    }

    // Métodos de estado
    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdateTimestamp();
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        Name = name;
        UpdateTimestamp();
    }

    // Métodos de resultado de juego
    public void WinBet(Money winnings)
    {
        if (winnings == null)
            throw new ArgumentNullException(nameof(winnings));

        AddToBalance(winnings);
    }

    public void ProcessPayout(Payout payout)
    {
        if (payout == null)
            throw new ArgumentNullException(nameof(payout));

        AddToBalance(payout.Amount);
        ClearBet();
    }

    // Factory methods
    public static Player Create(PlayerId playerId, string name, decimal initialBalance = 1000m)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        if (initialBalance < 0)
            throw new ArgumentException("Initial balance cannot be negative", nameof(initialBalance));

        return new Player(playerId, name, new Money(initialBalance));
    }

    public static Player Create(string name, decimal initialBalance = 1000m)
    {
        return Create(PlayerId.New(), name, initialBalance);
    }

    // Métodos de validación
    public bool CanPlaceBet(Money betAmount, Money minBet, Money maxBet)
    {
        if (betAmount == null || minBet == null || maxBet == null)
            return false;

        return CanAffordBet(betAmount) &&
               betAmount.Amount >= minBet.Amount &&
               betAmount.Amount <= maxBet.Amount;
    }

    public void ResetForNewRound()
    {
        ClearHandIds();
        ClearBet();
        SetActive(false);
        UpdateTimestamp();
    }
}