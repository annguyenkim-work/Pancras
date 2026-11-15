using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Pancras.Application.Employees;
using Pancras.Domain.Employees;

namespace Pancras.Web.Security;

public sealed class EmployeeSignIn(IHttpContextAccessor httpContextAccessor, TimeProvider clock)
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
        var properties = new AuthenticationProperties { IsPersistent = true };
        // Sliding expiry ghi đè IssuedUtc. Giữ mốc đăng nhập gốc để phiên không sống quá 12 giờ.
        properties.Items[EmployeeSessionClaims.SignedInAt] = await OriginalSignedInAtAsync(httpContext, session.Id)
            ?? clock.GetUtcNow().ToString("O");
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            properties);
    }

    private static async Task<string?> OriginalSignedInAtAsync(HttpContext httpContext, Guid employeeId)
    {
        var current = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!current.Succeeded
            || current.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) != employeeId.ToString()
            || current.Properties?.Items.TryGetValue(EmployeeSessionClaims.SignedInAt, out var signedInAt) != true
            || string.IsNullOrEmpty(signedInAt))
        {
            return null;
        }

        return signedInAt;
    }
}

public static class EmployeeSessionClaims
{
    public const string EmployeeCode = "EmployeeCode";

    public const string SecurityStamp = "EmployeeSecurityStamp";

    public const string LocationId = "StockLocationId";

    public const string SignedInAt = "SignedInAt";
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
