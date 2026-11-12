using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Web.Security;

namespace Pancras.Web.Pages.Account;

[AllowAnonymous]
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

        await signIn.SignInAsync(result.Value!);
        return RedirectToPage("/Index");
    }
}
