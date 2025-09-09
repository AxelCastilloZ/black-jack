using Microsoft.AspNetCore.Mvc;
using Blackjack.Application.Interfaces;
using Blackjack.Application.DTOs;

namespace Blackjack.WebApi.Controllers;

[ApiController]
[Route("rooms")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomDto dto)
    {
        var room = await _roomService.CreateRoomAsync(
            dto.Name, 
            dto.NumDecks, 
            dto.MinBet, 
            dto.MaxBet);
            
        return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, room);
    }

    [HttpGet]
    public async Task<IActionResult> ListRooms()
    {
        var rooms = await _roomService.ListRoomsAsync();
        return Ok(rooms);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRoom(int id)
    {
        var room = await _roomService.GetRoomDetailsAsync(id);
        if (room == null)
            return NotFound();
            
        return Ok(room);
    }

    [HttpPost("{id}/join")]
    public async Task<IActionResult> JoinRoom(int id, [FromBody] JoinRoomDto dto)
    {
        // For demo purposes, using userId = 1
        var success = await _roomService.JoinRoomAsync(id, 1, dto.Nickname, dto.SeatIndex);
        
        if (!success)
            return BadRequest("Seat already taken or invalid room");
            
        return Ok(new { Message = "Successfully joined room" });
    }

    [HttpPost("{id}/leave")]
    public async Task<IActionResult> LeaveRoom(int id, [FromBody] LeaveRoomRequest request)
    {
        var success = await _roomService.LeaveRoomAsync(id, request.PlayerId);
        
        if (!success)
            return BadRequest("Player not found or not in room");
            
        return Ok(new { Message = "Successfully left room" });
    }

    [HttpPost("{id}/close")]
    public async Task<IActionResult> CloseRoom(int id)
    {
        // For demo purposes, using userId = 1
        var success = await _roomService.CloseRoomAsync(id, 1);
        
        if (!success)
            return BadRequest("Room not found or not authorized");
            
        return Ok(new { Message = "Room closed successfully" });
    }
}

public class LeaveRoomRequest
{
    public int PlayerId { get; set; }
}
