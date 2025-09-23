
using BlackJack.Domain.Models.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Services.Common;

namespace BlackJack.Services.Table;

public interface ITableService
{
    // Métodos originales
    Task<Result<List<BlackjackTable>>> GetAvailableTablesAsync();
    Task<Result<BlackjackTable>> GetTableAsync(Guid tableId);
    Task<Result<BlackjackTable>> CreateTableAsync(string name);
    Task<Result> DeleteTableAsync(Guid tableId);

    // NUEVO: Método coordinado que incluye información de auto-betting
    Task<Result<List<EnhancedTableInfo>>> GetTablesWithAutoBettingInfoAsync();
}

/// <summary>
/// DTO que combina información de BlackjackTable y GameRoom para auto-betting
/// </summary>
public class EnhancedTableInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; }
    public decimal MinBet { get; set; }           // De BlackjackTable
    public decimal MaxBet { get; set; }           // De BlackjackTable
    public decimal MinBetPerRound { get; set; }   // De GameRoom - DINÁMICO
    public string Status { get; set; } = string.Empty;
    public bool HasActiveRoom { get; set; }       // Si tiene GameRoom asociado
    public string? RoomCode { get; set; }         // Código de sala si existe
}