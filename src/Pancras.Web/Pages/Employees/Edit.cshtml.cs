using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Common;
using Pancras.Application.Employees;

namespace Pancras.Web.Pages.Employees;

[Authorize(Roles = "Admin,Giám đốc")]
public class EditModel(GetEmployeeHandler getEmployee, UpdateEmployeeCommandHandler updateEmployee) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public UpdateEmployeeCommand Command { get; set; } = new();

    public string? Code { get; private set; }

    public EmployeeError Error { get; private set; }

    public IActionResult OnGet() => Redirect("/employees?edit=" + Id.ToString("D"));

    public async Task<IActionResult> OnPostAsync()
    {
        Command.Id = Id;
        var existing = await getEmployee.HandleAsync(Id, HttpContext.RequestAborted);
        Code = existing.Value?.Code;
        if (existing.IsFailure)
        {
            Error = existing.EmployeeError;
            return Page();
        }

        var result = await updateEmployee.HandleAsync(Command, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            Error = result.EmployeeError;
            return Page();
        }

        return Redirect("/employees");
    }
}
