using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Pancras.Application.Employees;

namespace Pancras.Web.Security;

public sealed class EmployeePrincipalValidator(IEmployeeRepository repository)
{
    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
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

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
