using Microsoft.AspNetCore.Mvc;
using Blackjack.Application.Interfaces;

namespace Blackjack.WebApi.Controllers;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("{id}/balance")]
    public async Task<IActionResult> GetBalance(int id)
    {
        var balance = await _userService.GetBalanceAsync(id);
        return Ok(new { UserId = id, Balance = balance });
    }

    [HttpPut("{id}/balance")]
    public async Task<IActionResult> UpdateBalance(int id, [FromBody] UpdateBalanceRequest request)
    {
        await _userService.UpdateBalanceAsync(id, request.Balance);
        return Ok(new { UserId = id, Balance = request.Balance, Message = "Balance updated successfully" });
    }
}

public class UpdateBalanceRequest
{
    public decimal Balance { get; set; }
}
