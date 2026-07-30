using E6CarSpa.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace E6CarSpa.Api.Auth;

/// <summary>
/// Requires the signed-in user to hold a permission. Applied on top of the global
/// "must be authenticated" fallback policy, so an endpoint carrying this is closed to anyone
/// without the bit — regardless of their role.
/// </summary>
/// <remarks>
/// The permission set travels in the token (see <see cref="JwtTokenService"/>). Changing someone's
/// permissions rotates their security stamp, which invalidates existing tokens, so a change takes
/// effect on their next request rather than whenever the token happens to expire.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirePermissionAttribute(Permission required) : Attribute, IAuthorizationFilter
{
    public const string ClaimType = "perms";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        // Unauthenticated requests are already handled by the fallback policy; leave the 401 to it.
        if (user.Identity?.IsAuthenticated != true) return;

        var raw = user.FindFirst(ClaimType)?.Value;
        var granted = int.TryParse(raw, out var bits) ? (Permission)bits : Permission.None;

        if (!granted.HasFlag(required))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Not permitted",
                Detail = $"Your account does not have the '{required}' permission. Ask an administrator."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
