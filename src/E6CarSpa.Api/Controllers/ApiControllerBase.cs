using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace E6CarSpa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the authenticated user, or null if unauthenticated.</summary>
    protected Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
