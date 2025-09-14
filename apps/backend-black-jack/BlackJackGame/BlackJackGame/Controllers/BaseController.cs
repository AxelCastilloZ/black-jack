using Microsoft.AspNetCore.Mvc;

namespace BlackJackGame.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseController : ControllerBase
{
    protected IActionResult HandleResult<T>(BlackJack.Services.Common.Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return BadRequest(new { error = result.Error });
    }

    protected IActionResult HandleResult(BlackJack.Services.Common.Result result)
    {
        if (result.IsSuccess)
            return Ok();

        return BadRequest(new { error = result.Error });
    }
}