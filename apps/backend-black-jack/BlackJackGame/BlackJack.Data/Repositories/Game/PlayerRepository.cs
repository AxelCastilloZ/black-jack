// BlackJack.Data.Repositories.Game/PlayerRepository.cs - CORREGIDO PARA VALUE OBJECTS
using Microsoft.EntityFrameworkCore;
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Betting;
using BlackJack.Data.Context;
using BlackJack.Data.Repositories.Common;

namespace BlackJack.Data.Repositories.Game;

public class PlayerRepository : Repository<Player>, IPlayerRepository
{
    public PlayerRepository(ApplicationDbContext context) : base(context)
    {
    }

    // CORREGIDO: Métodos existentes con comparaciones de value objects
    public async Task<Player?> GetByPlayerIdAsync(PlayerId playerId)
    {
        // Compare by underlying GUID to avoid EF Core owned-type member access issues
        return await _dbSet
            .FirstOrDefaultAsync(p => p.PlayerId.Value == playerId.Value);
    }

    public async Task<List<Player>> GetPlayersByTableAsync(Guid tableId)
    {
        // Como Seat no tiene BlackjackTableId directo, necesitamos hacer el query desde BlackjackTable
        var table = await _context.Set<BlackjackTable>()
            .Include(t => t.Seats)
            .ThenInclude(s => s.Player)
            .FirstOrDefaultAsync(t => t.Id == tableId);

        if (table == null)
            return new List<Player>();

        return table.Seats
            .Where(s => s.IsOccupied && s.Player != null)
            .Select(s => s.Player!)
            .ToList();
    }

    // CORREGIDOS: Métodos para apuestas automáticas con comparaciones correctas
    public async Task<List<Player>> GetPlayersByIdsAsync(List<PlayerId> playerIds)
    {
        if (playerIds == null || !playerIds.Any())
            return new List<Player>();

        var playerGuids = playerIds.Select(p => p.Value).ToList();

        return await _dbSet
            .Where(p => playerGuids.Contains(p.PlayerId.Value))
            .ToListAsync();
    }

    public async Task<Dictionary<Guid, Player>> GetPlayerDictionaryByIdsAsync(List<PlayerId> playerIds)
    {
        if (playerIds == null || !playerIds.Any())
            return new Dictionary<Guid, Player>();

        var players = await GetPlayersByIdsAsync(playerIds);

        return players.ToDictionary(p => p.PlayerId.Value, p => p);
    }

    public async Task<bool> HasSufficientFundsAsync(PlayerId playerId, Money amount)
    {
        var balance = await _dbSet
            .Where(p => p.PlayerId.Value == playerId.Value)
            .Select(p => p.Balance.Amount)
            .FirstOrDefaultAsync();

        return balance >= amount.Amount;
    }

    public async Task<Money?> GetPlayerBalanceAsync(PlayerId playerId)
    {
        var balanceAmount = await _dbSet
            .Where(p => p.PlayerId.Value == playerId.Value)
            .Select(p => p.Balance.Amount)
            .FirstOrDefaultAsync();

        return balanceAmount > 0 ? new Money(balanceAmount) : null;
    }

    public async Task<bool> UpdatePlayerBalanceAsync(PlayerId playerId, Money newBalance)
    {
        try
        {
            var player = await _dbSet.FirstOrDefaultAsync(p => p.PlayerId.Value == playerId.Value);
            if (player == null)
                return false;

            player.SetBalance(newBalance);
            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateMultiplePlayerBalancesAsync(Dictionary<PlayerId, Money> balanceUpdates)
    {
        if (balanceUpdates == null || !balanceUpdates.Any())
            return true;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var playerGuids = balanceUpdates.Keys.Select(p => p.Value).ToList();
            var players = await _dbSet
                .Where(p => playerGuids.Contains(p.PlayerId.Value))
                .ToListAsync();

            foreach (var player in players)
            {
                if (balanceUpdates.TryGetValue(player.PlayerId, out var newBalance))
                {
                    player.SetBalance(newBalance);
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    public async Task<bool> PlaceBetAsync(PlayerId playerId, Bet bet)
    {
        if (bet == null)
            return false;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var player = await _dbSet.FirstOrDefaultAsync(p => p.PlayerId.Value == playerId.Value);
            if (player == null)
                return false;

            // Validar fondos y estado
            if (!player.CanAffordBet(bet.Amount) || player.HasActiveBet)
                return false;

            // Colocar apuesta (automáticamente descuenta del balance)
            player.PlaceBet(bet);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    public async Task<bool> ClearPlayerBetAsync(PlayerId playerId)
    {
        try
        {
            var player = await _dbSet.FirstOrDefaultAsync(p => p.PlayerId.Value == playerId.Value);
            if (player == null)
                return false;

            player.ClearBet();
            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ClearMultiplePlayerBetsAsync(List<PlayerId> playerIds)
    {
        if (playerIds == null || !playerIds.Any())
            return true;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var playerGuids = playerIds.Select(p => p.Value).ToList();
            var players = await _dbSet
                .Where(p => playerGuids.Contains(p.PlayerId.Value) && p.CurrentBet != null)
                .ToListAsync();

            foreach (var player in players)
            {
                player.ClearBet();
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            return false;
        }
    }





    public async Task<Player?> GetByPlayerIdFreshAsync(PlayerId playerId)
    {
        return await _dbSet
            .AsNoTracking()  //  Fuerza consulta fresca desde BD
            .FirstOrDefaultAsync(p => p.PlayerId.Value == playerId.Value);
    }

    public async Task<Player?> GetByIdFreshAsync(Guid id)
    {
        return await _dbSet
            .AsNoTracking()  //  Fuerza consulta fresca desde BD
            .FirstOrDefaultAsync(p => p.Id == id);
    }





    public async Task<List<Player>> GetPlayersWithInsufficientFundsAsync(List<PlayerId> playerIds, Money requiredAmount)
    {
        if (playerIds == null || !playerIds.Any())
            return new List<Player>();

        var playerGuids = playerIds.Select(p => p.Value).ToList();

        return await _dbSet
            .Where(p => playerGuids.Contains(p.PlayerId.Value) &&
                       p.Balance.Amount < requiredAmount.Amount)
            .ToListAsync();
    }

    public async Task<List<PlayerId>> GetPlayersWithActiveBetsAsync(List<PlayerId> playerIds)
    {
        if (playerIds == null || !playerIds.Any())
            return new List<PlayerId>();

        var playerGuids = playerIds.Select(p => p.Value).ToList();

        return await _dbSet
            .Where(p => playerGuids.Contains(p.PlayerId.Value) && p.CurrentBet != null)
            .Select(p => p.PlayerId)
            .ToListAsync();
    }
}