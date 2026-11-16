using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Web.Security;

namespace Pancras.Web.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting("login")]
public class VerifyTwoFactorModel(VerifyTwoFactorCommandHandler verify, EmployeeSignIn signIn) : PageModel
{
    [BindProperty]
    public string? Code { get; set; }

    public LoginError Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        return await PendingEmployeeAsync() is null ? RedirectToPage("/Account/Login") : Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var employeeId = await PendingEmployeeAsync();
        if (employeeId is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var result = await verify.HandleAsync(
            new VerifyTwoFactorCommand { EmployeeId = employeeId.Value, Code = Code },
            HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            Error = result.Error;
            Code = null;
            if (result.Error == LoginError.InvalidCredentials)
            {
                await HttpContext.SignOutAsync(PendingTwoFactor.Scheme);
                return RedirectToPage("/Account/Login");
            }

            return Page();
        }

        await HttpContext.SignOutAsync(PendingTwoFactor.Scheme);
        await signIn.SignInAsync(result.Value!);
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostCancelAsync()
    {
        await HttpContext.SignOutAsync(PendingTwoFactor.Scheme);
        return RedirectToPage("/Account/Login");
    }

    private async Task<Guid?> PendingEmployeeAsync()
    {
        var pending = await HttpContext.AuthenticateAsync(PendingTwoFactor.Scheme);
        return pending.Succeeded && Guid.TryParse(pending.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
    }
}
