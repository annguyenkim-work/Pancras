using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Employees;
using Pancras.Web.Security;

namespace Pancras.Web.Pages.Employees;

[Authorize]
public class IndexModel(
    ListEmployeesHandler listEmployees,
    CreateEmployeeCommandHandler createEmployee,
    GetEmployeeHandler getEmployee,
    UpdateEmployeeCommandHandler updateEmployee) : PageModel
{
    public IReadOnlyList<EmployeeCard> Cards { get; private set; } = [];

    public string? Name { get; private set; }

    public string? Code { get; private set; }

    public string? Role { get; private set; }

    public string? Branch { get; private set; }

    public string? SignedInCode { get; private set; }

    public int PageNumber { get; private set; } = 1;

    public int PageCount { get; private set; } = 1;

    public bool ShowForm { get; private set; }

    public bool ShowEdit { get; private set; }

    public string? EditCode { get; private set; }

    public EmployeeError Error { get; private set; }

    public bool CanManage => User.IsInRole(EmployeeRole.Admin) || User.IsInRole(EmployeeRole.Director);

    [BindProperty]
    public CreateEmployeeCommand Command { get; set; } = new();

    [BindProperty]
    public UpdateEmployeeCommand EditCommand { get; set; } = new();

    public async Task OnGetAsync(string? name, string? code, string? role, string? branch, string? form, Guid? edit, int pageNumber = 1)
    {
        ShowForm = form == "1" && edit is null;
        await LoadAsync(name, code, role, branch, pageNumber, HttpContext.RequestAborted);
        if (edit is Guid employeeId && employeeId != Guid.Empty && CanManage)
        {
            await OpenEditAsync(employeeId, HttpContext.RequestAborted);
        }
    }

    public async Task<IActionResult> OnPostCreateAsync(string? name, string? code, string? role, string? branch, CancellationToken cancellationToken)
    {
        if (!CanManage)
        {
            return Forbid();
        }

        var result = await createEmployee.HandleAsync(Command, cancellationToken);
        if (result.IsFailure)
        {
            ShowForm = true;
            Error = result.EmployeeError;
            await LoadAsync(name, code, role, branch, 1, cancellationToken);
            return Page();
        }

        return Redirect(ListUrl());
    }

    public async Task<IActionResult> OnPostUpdateAsync(string? name, string? code, string? role, string? branch, int pageNumber, CancellationToken cancellationToken)
    {
        if (!CanManage)
        {
            return Forbid();
        }

        await LoadAsync(name, code, role, branch, pageNumber, cancellationToken);
        var existing = await getEmployee.HandleAsync(EditCommand.Id, cancellationToken);
        EditCode = existing.Value?.Code;
        if (existing.IsFailure)
        {
            ShowEdit = true;
            Error = existing.EmployeeError;
            return Page();
        }

        var result = await updateEmployee.HandleAsync(EditCommand, cancellationToken);
        if (result.IsFailure)
        {
            ShowEdit = true;
            Error = result.EmployeeError;
            return Page();
        }

        return Redirect(ListUrl());
    }

    public string PreviewCode =>
        EmployeeCodeGenerator.Attempts(Command.FullName ?? string.Empty).Select(attempt => attempt.Code).FirstOrDefault() ?? string.Empty;

    public string ListUrl()
    {
        var parts = new List<string>();
        Add(parts, "name", Name);
        Add(parts, "code", Code);
        Add(parts, "role", Role);
        Add(parts, "branch", Branch);
        if (PageNumber > 1)
        {
            parts.Add("pageNumber=" + PageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return parts.Count == 0 ? "/employees" : "/employees?" + string.Join("&", parts);
    }

    public string FormUrl()
    {
        var list = ListUrl();
        return list.Contains('?', StringComparison.Ordinal) ? list + "&form=1" : list + "?form=1";
    }

    public string EditUrl(Guid employeeId)
    {
        var list = ListUrl();
        return list.Contains('?', StringComparison.Ordinal)
            ? list + "&edit=" + employeeId.ToString("D")
            : list + "?edit=" + employeeId.ToString("D");
    }

    private async Task OpenEditAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var result = await getEmployee.HandleAsync(employeeId, cancellationToken);
        ShowEdit = true;
        if (result.IsFailure)
        {
            Error = result.EmployeeError;
            return;
        }

        EditCode = result.Value!.Code;
        EditCommand = new UpdateEmployeeCommand
        {
            Id = result.Value.Id,
            FullName = result.Value.FullName,
            Role = result.Value.Role,
            Email = result.Value.Email,
            Phone = result.Value.Phone,
            Branch = result.Value.Branch,
            JobTitle = result.Value.JobTitle,
            Department = result.Value.Department
        };
    }

    private static void Add(List<string> parts, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add(key + "=" + Uri.EscapeDataString(value));
        }
    }

    private async Task LoadAsync(string? name, string? code, string? role, string? branch, int page, CancellationToken cancellationToken)
    {
        Name = name;
        Code = code;
        Role = role;
        Branch = branch;
        SignedInCode = User.FindFirstValue(EmployeeSessionClaims.EmployeeCode);
        var cards = await listEmployees.HandleAsync(name, code, role, branch, cancellationToken);
        PageCount = Math.Max(1, (cards.Count + ListEmployeesHandler.PageSize - 1) / ListEmployeesHandler.PageSize);
        PageNumber = page < 1 ? 1 : page > PageCount ? PageCount : page;
        Cards = cards.Skip((PageNumber - 1) * ListEmployeesHandler.PageSize).Take(ListEmployeesHandler.PageSize).ToArray();
    }
}
