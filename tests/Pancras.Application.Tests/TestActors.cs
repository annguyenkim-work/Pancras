using Pancras.Domain.Employees;

namespace Pancras.Application.Tests;

internal static class TestActors
{
    public static EmployeeActor Admin { get; } = new(Guid.NewGuid(), EmployeeRole.Admin, null);

    public static EmployeeActor Director { get; } = new(Guid.NewGuid(), EmployeeRole.Director, null);

    public static EmployeeActor Accountant { get; } = new(Guid.NewGuid(), EmployeeRole.Accountant, null);

    public static EmployeeActor Keeper(Guid location) => new(Guid.NewGuid(), EmployeeRole.WarehouseKeeper, location);

    public static EmployeeActor Lead(Guid location) => new(Guid.NewGuid(), EmployeeRole.ShowroomWarehouseLead, location);

    public static EmployeeActor Sales(Guid location) => new(Guid.NewGuid(), EmployeeRole.Sales, location);
}
