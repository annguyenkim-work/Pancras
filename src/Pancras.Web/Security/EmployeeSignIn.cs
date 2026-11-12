using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Pancras.Application.Employees;

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
}
