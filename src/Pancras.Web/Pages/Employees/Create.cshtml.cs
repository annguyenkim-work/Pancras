using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Common;
using Pancras.Application.Employees;

namespace Pancras.Web.Pages.Employees;

[Authorize(Roles = "Admin,Giám đốc")]
public class CreateModel(CreateEmployeeCommandHandler handler) : PageModel
{
    [BindProperty]
    public CreateEmployeeCommand Command { get; set; } = new();

    public EmployeeError Error { get; private set; }

    public string? CreatedCode { get; private set; }

    public Guid? CreatedId { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await handler.HandleAsync(Command, HttpContext.RequestAborted);
        if (result.IsFailure)
        {
            Error = result.EmployeeError;
            return Page();
        }

        CreatedCode = result.Value!.Code;
        CreatedId = result.Value.Id;
        return Page();
    }
}
