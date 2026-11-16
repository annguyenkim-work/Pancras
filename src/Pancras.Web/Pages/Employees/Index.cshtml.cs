using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Application.Locations;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;
using Pancras.Web.Security;

namespace Pancras.Web.Pages.Employees;

[Authorize(Roles = EmployeeAccess.ListRoles)]
public class IndexModel(
    ListEmployeesHandler listEmployees,
    CreateEmployeeCommandHandler createEmployee,
    GetEmployeeHandler getEmployee,
    UpdateEmployeeCommandHandler updateEmployee,
    SetEmployeeActiveCommandHandler setActive,
    UnlockEmployeeCommandHandler unlock,
    ResetTwoFactorCommandHandler resetTwoFactor,
    ListLocationsHandler listLocations) : PageModel
{
    public IReadOnlyList<EmployeeCard> Cards { get; private set; } = [];

    public IReadOnlyList<StockLocation> Locations { get; private set; } = [];

    public string? Search { get; private set; }

    public string? Role { get; private set; }

    public Guid? LocationFilter { get; private set; }

    public string? SignedInCode { get; private set; }

    public int PageNumber { get; private set; } = 1;

    public int PageCount { get; private set; } = 1;

    public bool ShowForm { get; private set; }

    public bool ShowEdit { get; private set; }

    public string? EditCode { get; private set; }

    public string? EditTargetRole { get; private set; }

    public EmployeeError Error { get; private set; }

    public EmployeeActor Actor => User.ToActor();

    public bool CanCreate => EmployeeAccess.CanCreateAny(Actor);

    public bool IsLead => Actor.Role == EmployeeRole.ShowroomWarehouseLead;

    public IReadOnlyList<string> CreatableRoles => EmployeeAccess.CreatableRoles(Actor);

    public bool CanEditPersonal => EditCommand.Id != Guid.Empty && EmployeeAccess.CanEditPersonal(Actor, EditCommand.Id);

    public bool CanEditAssignment =>
        EditCommand.Id != Guid.Empty && EmployeeAccess.CanEditAssignment(Actor, EditCommand.Id, EditTargetRole);

    public bool CanResetPassword => EmployeeAccess.CanResetPassword(Actor);

    public bool CanEdit(EmployeeCard card) =>
        (EmployeeAccess.CanEditPersonal(Actor, card.Id) && card.Id != Actor.Id)
        || EmployeeAccess.CanEditAssignment(Actor, card.Id, card.Role);

    public bool CanToggle(EmployeeCard card) => EmployeeAccess.CanSetActive(Actor, card.Id, card.Role);

    public IReadOnlyList<StockLocation> LocationChoices =>
        IsLead && Actor.LocationId is Guid own ? Locations.Where(location => location.Id == own).ToArray() : Locations;

    [BindProperty]
    public CreateEmployeeCommand Command { get; set; } = new();

    [BindProperty]
    public UpdateEmployeeCommand EditCommand { get; set; } = new();

    public async Task OnGetAsync(string? q, string? role, Guid? location, string? form, Guid? edit, int pageNumber = 1)
    {
        ShowForm = form == "1" && edit is null && CanCreate;
        if (ShowForm && IsLead)
        {
            Command.StockLocationId = Actor.LocationId;
        }

        await LoadAsync(q, role, location, pageNumber, HttpContext.RequestAborted);
        if (edit is Guid employeeId && employeeId != Guid.Empty)
        {
            await OpenEditAsync(employeeId, HttpContext.RequestAborted);
        }
    }

    public async Task<IActionResult> OnPostCreateAsync(string? q, string? role, Guid? location, CancellationToken cancellationToken)
    {
        if (!EmployeeAccess.CanCreateAny(Actor))
        {
            return Forbid();
        }

        var result = await createEmployee.HandleAsync(Command, Actor, cancellationToken);
        if (result.IsFailure)
        {
            ShowForm = true;
            Error = result.EmployeeError;
            await LoadAsync(q, role, location, 1, cancellationToken);
            return Page();
        }

        return Redirect(ListUrl());
    }

    public async Task<IActionResult> OnPostUpdateAsync(string? q, string? role, Guid? location, int pageNumber, CancellationToken cancellationToken)
    {
        await LoadAsync(q, role, location, pageNumber, cancellationToken);
        var existing = await getEmployee.HandleAsync(EditCommand.Id, Actor, cancellationToken);
        EditCode = existing.Value?.Code;
        EditTargetRole = existing.Value?.Role;
        if (existing.IsFailure)
        {
            ShowEdit = true;
            Error = existing.EmployeeError;
            return Page();
        }

        var result = await updateEmployee.HandleAsync(EditCommand, Actor, cancellationToken);
        if (result.IsFailure)
        {
            ShowEdit = true;
            Error = result.EmployeeError;
            return Page();
        }

        return Redirect(ListUrl());
    }

    public async Task<IActionResult> OnPostSetActiveAsync(Guid id, bool active, string? q, string? role, Guid? location, int pageNumber, CancellationToken cancellationToken)
    {
        var result = await setActive.HandleAsync(id, active, Actor, cancellationToken);
        if (result.IsFailure)
        {
            Error = result.EmployeeError;
            await LoadAsync(q, role, location, pageNumber, cancellationToken);
            return Page();
        }

        Search = q;
        Role = role;
        LocationFilter = location;
        PageNumber = pageNumber;
        return Redirect(ListUrl());
    }

    public Task<IActionResult> OnPostUnlockAsync(Guid id, string? q, string? role, Guid? location, int pageNumber, CancellationToken cancellationToken) =>
        AdminActionAsync(unlock.HandleAsync(id, Actor, cancellationToken), q, role, location, pageNumber, cancellationToken);

    public Task<IActionResult> OnPostResetTwoFactorAsync(Guid id, string? q, string? role, Guid? location, int pageNumber, CancellationToken cancellationToken) =>
        AdminActionAsync(resetTwoFactor.HandleAsync(id, Actor, cancellationToken), q, role, location, pageNumber, cancellationToken);

    private async Task<IActionResult> AdminActionAsync(
        Task<Result<EmployeeProfile>> action,
        string? q,
        string? role,
        Guid? location,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        var result = await action;
        Search = q;
        Role = role;
        LocationFilter = location;
        PageNumber = pageNumber;
        if (result.IsFailure)
        {
            Error = result.EmployeeError;
            await LoadAsync(q, role, location, pageNumber, cancellationToken);
            return Page();
        }

        return Redirect(ListUrl());
    }

    public bool IsLocked(EmployeeCard card) => card.LockedUntil is DateTimeOffset until && until > DateTimeOffset.UtcNow;

    public string PreviewCode =>
        EmployeeCodeGenerator.Attempts(Command.FullName ?? string.Empty).Select(attempt => attempt.Code).FirstOrDefault() ?? string.Empty;

    public string ListUrl()
    {
        var parts = new List<string>();
        Add(parts, "q", Search);
        Add(parts, "role", Role);
        Add(parts, "location", LocationFilter?.ToString("D"));
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
        var result = await getEmployee.HandleAsync(employeeId, Actor, cancellationToken);
        ShowEdit = true;
        if (result.IsFailure)
        {
            Error = result.EmployeeError;
            return;
        }

        EditCode = result.Value!.Code;
        EditTargetRole = result.Value.Role;
        EditCommand = new UpdateEmployeeCommand
        {
            Id = result.Value.Id,
            Version = result.Value.Version,
            FullName = result.Value.FullName,
            Role = result.Value.Role,
            Email = result.Value.Email,
            Phone = result.Value.Phone,
            Address = result.Value.Address,
            StockLocationId = result.Value.StockLocationId,
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

    private async Task LoadAsync(string? q, string? role, Guid? location, int page, CancellationToken cancellationToken)
    {
        Search = q;
        Role = role;
        LocationFilter = location;
        SignedInCode = User.FindFirstValue(EmployeeSessionClaims.EmployeeCode);
        Locations = await listLocations.HandleAsync(cancellationToken);
        var cards = await listEmployees.HandleAsync(Actor, q, role, location, cancellationToken);
        PageCount = Math.Max(1, (cards.Count + ListEmployeesHandler.PageSize - 1) / ListEmployeesHandler.PageSize);
        PageNumber = page < 1 ? 1 : page > PageCount ? PageCount : page;
        Cards = cards.Skip((PageNumber - 1) * ListEmployeesHandler.PageSize).Take(ListEmployeesHandler.PageSize).ToArray();
    }
}
