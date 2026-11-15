using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Pancras.Application.Employees;

namespace Pancras.Web.Security;

public sealed class EmployeePrincipalValidator(
    IEmployeeRepository repository,
    TimeProvider clock,
    ILogger<EmployeePrincipalValidator> logger)
{
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(12);

    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        if (SessionTooOld(context.Properties, clock.GetUtcNow()))
        {
            logger.LogInformation("Signed-in session exceeded the 12 hour absolute lifetime.");
            await RejectAsync(context);
            return;
        }

        var principal = context.Principal;
        var idValue = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var stamp = principal?.FindFirstValue(EmployeeSessionClaims.SecurityStamp);
        if (!Guid.TryParse(idValue, out var id) || string.IsNullOrEmpty(stamp))
        {
            await RejectAsync(context);
            return;
        }

        var employee = await repository.GetByIdReadOnlyAsync(id, context.HttpContext.RequestAborted);
        if (employee is null || !employee.IsActive || !string.Equals(employee.SecurityStamp, stamp, StringComparison.Ordinal))
        {
            await RejectAsync(context);
        }
    }

    internal static bool SessionTooOld(AuthenticationProperties properties, DateTimeOffset now)
    {
        var started = ReadSignedInAt(properties) ?? properties.IssuedUtc;
        return started is DateTimeOffset issued && now - issued >= AbsoluteLifetime;
    }

    private static DateTimeOffset? ReadSignedInAt(AuthenticationProperties properties)
    {
        if (!properties.Items.TryGetValue(EmployeeSessionClaims.SignedInAt, out var value) || string.IsNullOrEmpty(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
