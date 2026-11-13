using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Pancras.Application.Employees;
using Pancras.Domain.Employees;

namespace Pancras.Web.Security;

public sealed class EmployeeSignIn(IHttpContextAccessor httpContextAccessor)
{
    public async Task SignInAsync(EmployeeSession session)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.Id.ToString()),
            new(ClaimTypes.Name, session.FullName),
            new(ClaimTypes.Role, session.Role),
            new(EmployeeSessionClaims.EmployeeCode, session.Code),
            new(EmployeeSessionClaims.SecurityStamp, session.SecurityStamp)
        };
        if (session.LocationId is Guid locationId)
        {
            claims.Add(new Claim(EmployeeSessionClaims.LocationId, locationId.ToString("D")));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HTTP context is available.");
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }
}

public static class EmployeeSessionClaims
{
    public const string EmployeeCode = "EmployeeCode";

    public const string SecurityStamp = "EmployeeSecurityStamp";

    public const string LocationId = "StockLocationId";
}

public static class PrincipalExtensions
{
    public static EmployeeActor ToActor(this ClaimsPrincipal user)
    {
        _ = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id);
        Guid? location = Guid.TryParse(user.FindFirstValue(EmployeeSessionClaims.LocationId), out var locationId)
            ? locationId
            : null;
        return new EmployeeActor(id, user.FindFirstValue(ClaimTypes.Role), location);
    }
}
