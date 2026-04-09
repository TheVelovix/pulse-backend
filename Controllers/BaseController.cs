using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace pulse_backend.Controllers;

public class BaseController : ControllerBase
{
    protected long? GetUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(value, out var id) ? id : null;
    }
}
