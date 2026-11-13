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
public class LoginModel(LoginEmployeeCommandHandler login, EmployeeSignIn signIn) : PageModel
{
    [BindProperty]
    public LoginEmployeeCommand Command { get; set; } = new();

    public LoginError Error { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await login.HandleAsync(Command, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            Error = result.LoginError;
            return Page();
        }

        if (result.Value!.RequiresTwoFactor)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, result.Value.Id.ToString())],
                PendingTwoFactor.Scheme);
            await HttpContext.SignInAsync(PendingTwoFactor.Scheme, new ClaimsPrincipal(identity));
            return RedirectToPage("/Account/VerifyTwoFactor");
        }

        await signIn.SignInAsync(result.Value);
        return RedirectToPage("/Index");
    }
}
