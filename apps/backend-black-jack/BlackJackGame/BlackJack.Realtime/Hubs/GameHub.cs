// BlackJack.Realtime.Hubs/GameHub.cs - Versión completa con ResetTable
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using BlackJack.Services.Game;
using BlackJack.Domain.Models.Users;
using BlackJack.Domain.Models.Game;
using System.Security.Claims;

namespace BlackJack.Realtime.Hubs;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GameHub : Hub
{
    private readonly IGameService _gameService;

    public GameHub(IGameService gameService)
    {
        _gameService = gameService;
    }

    private PlayerId GetCurrentPlayerId()
    {
        // Intentar obtener el ID del usuario desde los claims
        var userIdClaim = Context.User?.FindFirst("sub")?.Value
                       ?? Context.User?.FindFirst("id")?.Value
                       ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userIdClaim != null && Guid.TryParse(userIdClaim, out var userId))
        {
            return PlayerId.From(userId);
        }

        // Fallback: usar un GUID basado en el ConnectionId para consistencia
        var connectionGuid = Guid.NewGuid();
        return PlayerId.From(connectionGuid);
    }

    public async Task JoinTable(string tableId)
    {
        try
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Table_{tableId}");
            await Clients.Group($"Table_{tableId}").SendAsync("PlayerJoined", Context.ConnectionId);

            // FORZAR envío de estado después de unirse a la mesa
            await NotifyGameState(tableId);
            Console.WriteLine($"[GameHub] JoinTable completed - estado enviado");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameHub] Error en JoinTable: {ex.Message}");
            throw new HubException($"Error al unirse a la mesa: {ex.Message}");
        }
    }

    public async Task LeaveTable(string tableId)
    {
        try
        {
            if (!Guid.TryParse(tableId, out var tableGuid))
                throw new HubException("ID de mesa inválido");

            var playerId = GetCurrentPlayerId();
            Console.WriteLine($"[GameHub] LeaveTable iniciado por {playerId} para mesa {tableId}");

            // Llamar al GameService para salir de la mesa
            var result = await _gameService.LeaveTableAsync(TableId.From(tableGuid), playerId);

            if (!result.IsSuccess)
                throw new HubException(result.Error ?? "No se pudo salir de la mesa");

            // Remover del grupo de la mesa
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Table_{tableId}");

            // Notificar a todos que el jugador se fue
            await Clients.Group($"Table_{tableId}").SendAsync("PlayerLeft", Context.ConnectionId, playerId.ToString());

            Console.WriteLine($"[GameHub] LeaveTable exitoso - enviando estado actualizado");

            // FORZAR envío de estado después de salir
            await NotifyGameState(tableId);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameHub] Error en LeaveTable: {ex.Message}");
            throw new HubException($"Error al salir de la mesa: {ex.Message}");
        }
    }

    /// <summary>
    /// Método específico para salir de un asiento (más claro que LeaveTable)
    /// </summary>
    public async Task LeaveSeat(string tableId)
    {
        try
        {
            if (!Guid.TryParse(tableId, out var tableGuid))
                throw new HubException("ID de mesa inválido");

            var playerId = GetCurrentPlayerId();
            Console.WriteLine($"[GameHub] LeaveSeat iniciado por {playerId} para mesa {tableId}");

            // Llamar al GameService para salir de la mesa
            var result = await _gameService.LeaveTableAsync(TableId.From(tableGuid), playerId);

            if (!result.IsSuccess)
                throw new HubException(result.Error ?? "No se pudo liberar el asiento");

            // Notificar a todos que el jugador liberó su asiento
            await Clients.Group($"Table_{tableId}").SendAsync("PlayerLeftSeat", Context.ConnectionId, playerId.ToString());

            Console.WriteLine($"[GameHub] LeaveSeat exitoso - enviando estado actualizado");

            // FORZAR envío de estado después de liberar asiento
            await Task.Delay(100); // Pequeño delay para asegurar que el backend procese
            await NotifyGameState(tableId);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameHub] Error en LeaveSeat: {ex.Message}");
            throw new HubException($"Error al liberar asiento: {ex.Message}");
        }
    }

    /// <summary>
    /// Método para resetear una mesa que está atascada en InProgress
    /// </summary>
    public async Task ResetTable(string tableId)
    {
        try
        {
            if (!Guid.TryParse(tableId, out var tableGuid))
                throw new HubException("ID de mesa inválido");

            var playerId = GetCurrentPlayerId();
            Console.WriteLine($"[GameHub] ResetTable iniciado por {playerId} para mesa {tableId}");

            // Llamar al GameService para resetear la mesa
            var result = await _gameService.ResetTableAsync(TableId.From(tableGuid));

            if (!result.IsSuccess)
                throw new HubException(result.Error ?? "No se pudo resetear la mesa");

            // Notificar a todos que la mesa se reseteo
            await Clients.Group($"Table_{tableId}").SendAsync("TableReset", new
            {
                tableId = tableId,
                resetBy = playerId.ToString(),
                timestamp = DateTime.UtcNow,
                message = "Mesa reseteada - lista para nueva partida"
            });

            Console.WriteLine($"[GameHub] ResetTable exitoso - enviando estado actualizado");

            // Enviar estado actualizado
            await Task.Delay(100);
            await NotifyGameState(tableId);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameHub] Error en ResetTable: {ex.Message}");
            throw new HubException($"Error al resetear mesa: {ex.Message}");
        }
    }

    public async Task SendMessage(string tableId, string message)
    {
        await Clients.Group($"Table_{tableId}").SendAsync("ReceiveMessage", Context.ConnectionId, message);
    }

    // Método para unirse a un asiento específico
    public async Task JoinSeat(string tableId, int seatPosition)
    {
        try
        {
            if (!Guid.TryParse(tableId, out var tableGuid))
                throw new HubException("ID de mesa inválido");

            if (seatPosition < 0 || seatPosition >= 6)
                throw new HubException("Posición de asiento inválida (0-5)");

            var playerId = GetCurrentPlayerId();
            Console.WriteLine($"[GameHub] JoinSeat iniciado: mesa={tableId}, posición={seatPosition}, jugador={playerId}");

            var result = await _gameService.JoinTableAsync(TableId.From(tableGuid), playerId, seatPosition);

            if (!result.IsSuccess)
                throw new HubException(result.Error ?? "No se pudo unir al asiento");

            // Asegurar que está en el grupo de la mesa
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Table_{tableId}");

            // Notificar que se unió al asiento específico
            await Clients.Group($"Table_{tableId}").SendAsync("PlayerJoinedSeat", Context.ConnectionId, seatPosition, playerId.ToString());

            Console.WriteLine($"[GameHub] JoinSeat exitoso - enviando estado actualizado");

            // FORZAR envío de estado después de unirse al asiento
            await Task.Delay(100); // Pequeño delay para asegurar que el backend procese
            await NotifyGameState(tableId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameHub] Error en JoinSeat: {ex.Message}");
            throw new HubException($"Error al unirse al asiento: {ex.Message}");
        }
    }

    // Método para iniciar partida usando el GameService real
    public async Task StartRound(string tableId)
    {
        try
        {
            if (!Guid.TryParse(tableId, out var tableGuid))
                throw new HubException("ID de mesa inválido");

            var playerId = GetCurrentPlayerId();
            Console.WriteLine($"[GameHub] StartRound iniciado por {playerId} para mesa {tableId}");

            // Llamar al GameService para iniciar la ronda (solo necesita TableId)
            var result = await _gameService.StartRoundAsync(TableId.From(tableGuid));

            if (!result.IsSuccess)
                throw new HubException(result.Error ?? "No se pudo iniciar la partida");

            // Asegurar que el iniciador está en el grupo
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Table_{tableId}");

            // Notificar a todos en la mesa que la partida comenzó
            await Clients.Group($"Table_{tableId}").SendAsync("RoundStarted", new
            {
                tableId = tableId,
                startedBy = playerId.ToString(),
                timestamp = DateTime.UtcNow,
                message = "¡La partida ha comenzado! Cartas repartidas."
            });

            Console.WriteLine($"[GameHub] StartRound exitoso - enviando estado actualizado");

            // FORZAR envío de estado después de iniciar ronda
            await Task.Delay(100);
            await NotifyGameState(tableId);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameHub] Error en StartRound: {ex.Message}");
            throw new HubException($"Error al iniciar partida: {ex.Message}");
        }
    }

    // Método para enviar el estado del juego (incluye cartas) - MEJORADO
    private async Task NotifyGameState(string tableId)
    {
        try
        {
            Console.WriteLine($"[GameHub] NotifyGameState iniciado para mesa {tableId}");

            var tableResult = await _gameService.GetTableAsync(TableId.From(Guid.Parse(tableId)));
            if (tableResult.IsSuccess)
            {
                var table = tableResult.Value;

                // Log detallado del estado
                Console.WriteLine($"[GameHub] Estado de la mesa: {table.Status}");
                Console.WriteLine($"[GameHub] Jugadores en asientos: {table.Seats.Count(s => s.IsOccupied)}");

                var gameState = new
                {
                    id = table.Id.ToString(),
                    status = table.Status.ToString(),
                    roundNumber = table.RoundNumber,
                    players = table.Seats.Where(s => s.IsOccupied && s.Player != null)
                                        .Select(s => new
                                        {
                                            id = s.Player.PlayerId.ToString(),
                                            displayName = s.Player.Name,
                                            balance = s.Player.Balance.Amount,
                                            currentBet = s.Player.CurrentBet?.Amount.Amount ?? 0,
                                            position = s.Position,
                                            isActive = s.Player.IsActive,
                                            // Incluir cartas de la mano del jugador
                                            hand = s.Player.Hands.FirstOrDefault() != null ? new
                                            {
                                                cards = s.Player.Hands.First().Cards.Select(c => new
                                                {
                                                    suit = c.Suit.ToString(),
                                                    rank = c.Rank.ToString(),
                                                    value = c.GetValue(),
                                                    isHidden = false
                                                }).ToArray(),
                                                handValue = s.Player.Hands.First().Value,
                                                isBusted = s.Player.Hands.First().Status.ToString() == "Bust",
                                                hasBlackjack = s.Player.Hands.First().Status.ToString() == "Blackjack"
                                            } : null
                                        }).ToArray(),
                    minBet = table.MinBet.Amount,
                    maxBet = table.MaxBet.Amount,
                    pot = table.Seats.Where(s => s.IsOccupied && s.Player?.CurrentBet != null)
                                   .Sum(s => s.Player.CurrentBet.Amount.Amount),
                    // Incluir cartas del dealer
                    dealer = new
                    {
                        hand = table.DealerHand.Cards.Select((c, index) => new
                        {
                            suit = c.Suit.ToString(),
                            rank = c.Rank.ToString(),
                            value = c.GetValue(),
                            // La primera carta del dealer visible, la segunda oculta hasta el final
                            isHidden = index == 1 && table.Status.ToString() == "InProgress"
                        }).ToArray(),
                        handValue = table.Status.ToString() == "InProgress" ?
                            table.DealerHand.Cards.Take(1).Sum(c => c.GetValue()) : // Solo mostrar valor de carta visible
                            table.DealerHand.Value, // Mostrar valor completo cuando termine la ronda
                        isBusted = table.DealerHand.Status.ToString() == "Bust",
                        hasBlackjack = table.DealerHand.Status.ToString() == "Blackjack"
                    }
                };

                Console.WriteLine($"[GameHub] Enviando GameStateUpdated - {gameState.players.Length} jugadores");
                await Clients.Group($"Table_{tableId}").SendAsync("GameStateUpdated", gameState);
                Console.WriteLine($"[GameHub] GameStateUpdated enviado exitosamente");
            }
            else
            {
                Console.WriteLine($"[GameHub] Error obteniendo estado de mesa: {tableResult.Error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameHub] Error enviando estado del juego: {ex.Message}");
            Console.WriteLine($"[GameHub] Stack trace: {ex.StackTrace}");
        }
    }
}