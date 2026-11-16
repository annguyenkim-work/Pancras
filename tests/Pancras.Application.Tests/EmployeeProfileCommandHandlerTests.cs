using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

public class EmployeeProfileCommandHandlerTests
{
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeEmployeeRepository _repository = new();
    private readonly FakeLocationRepository _locations = new();
    private readonly StockLocation _kho = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
    private readonly StockLocation _showroom = StockLocation.Create("Showroom B", StockLocationKind.Showroom);

    public EmployeeProfileCommandHandlerTests()
    {
        _locations.Locations.Add(_kho);
        _locations.Locations.Add(_showroom);
    }

    private static EmployeeActor Admin() => new(Guid.NewGuid(), EmployeeRole.Admin, null);

    private static EmployeeActor Director() => new(Guid.NewGuid(), EmployeeRole.Director, null);

    private static EmployeeActor Lead(Guid location) => new(Guid.NewGuid(), EmployeeRole.ShowroomWarehouseLead, location);

    private Employee Add(string name, string code, string role, Guid? location = null, string password = "Secret12")
    {
        var employee = Employee.Create(
            name, code, code.ToUpperInvariant(), _hasher.Hash(password), role,
            "x@example.com", "0901000000", location, "Chức vụ");
        _repository.Employees.Add(employee);
        return employee;
    }

    private CreateEmployeeCommandHandler CreateHandler() =>
        new(_repository, _locations, _hasher, NullLogger<CreateEmployeeCommandHandler>.Instance);

    private UpdateEmployeeCommandHandler UpdateHandler() =>
        new(_repository, _locations, _hasher, NullLogger<UpdateEmployeeCommandHandler>.Instance);

    private static CreateEmployeeCommand CreateCommand(string fullName, string role = EmployeeRole.Accountant, Guid? location = null) =>
        new()
        {
            FullName = fullName,
            Password = "Secret12",
            Role = role,
            Email = "an@example.com",
            Phone = "0901000000",
            StockLocationId = location,
            JobTitle = "Chuyên viên",
            Department = "Phòng"
        };

    private static UpdateEmployeeCommand UpdateFrom(Employee employee) =>
        new()
        {
            Id = employee.Id,
            FullName = employee.FullName,
            Role = employee.Role,
            Email = employee.Email,
            Phone = employee.Phone,
            Address = employee.Address,
            StockLocationId = employee.StockLocationId,
            JobTitle = employee.JobTitle,
            Department = employee.Department
        };

    [Fact]
    public async Task UpdateWithAStaleVersion_IsRejected_AndChangesNothing()
    {
        var employee = Add("Nguyễn An", "an", EmployeeRole.Accountant);
        var command = UpdateFrom(employee);
        command.Version = employee.Version + 1;
        command.FullName = "Tên khác";

        var result = await UpdateHandler().HandleAsync(command, Admin(), CancellationToken.None);

        Assert.Equal(EmployeeError.VersionConflict, result.Error);
        Assert.Equal("Nguyễn An", employee.FullName);
    }

    [Fact]
    public async Task UpdateLostToAConcurrentSave_IsReportedAsAVersionConflict()
    {
        var employee = Add("Nguyễn An", "an", EmployeeRole.Accountant);
        var command = UpdateFrom(employee);
        command.Department = "Phòng mới";
        _repository.NextSaveOutcome = Pancras.Application.Common.SaveOutcome.Conflict;

        var result = await UpdateHandler().HandleAsync(command, Admin(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmployeeError.VersionConflict, result.Error);
    }

    [Fact]
    public async Task DeactivateLostToAConcurrentSave_IsReportedAsAVersionConflict()
    {
        Add("Quản Trị", "admin", EmployeeRole.Admin);
        var accountant = Add("Nguyễn Kim An", "AnNK", EmployeeRole.Accountant);
        _repository.NextSaveOutcome = Pancras.Application.Common.SaveOutcome.Conflict;
        var handler = new SetEmployeeActiveCommandHandler(_repository, NullLogger<SetEmployeeActiveCommandHandler>.Instance);

        var result = await handler.HandleAsync(accountant.Id, false, Admin(), CancellationToken.None);

        Assert.Equal(EmployeeError.VersionConflict, result.Error);
    }

    [Fact]
    public async Task CreateWithoutPassword_DoesNotInsert()
    {
        var command = CreateCommand("Nguyễn Kim An");
        command.Password = "";

        var result = await CreateHandler().HandleAsync(command, Admin(), CancellationToken.None);

        Assert.Equal(EmployeeError.WeakPassword, result.Error);
        Assert.Empty(_repository.Added);
    }

    [Fact]
    public async Task CreateWithShortPassword_IsRejected()
    {
        var command = CreateCommand("Nguyễn Kim An");
        command.Password = "Abc1234";

        var result = await CreateHandler().HandleAsync(command, Admin(), CancellationToken.None);

        Assert.Equal(EmployeeError.WeakPassword, result.Error);
    }

    [Fact]
    public async Task CreateNguyenKimAn_AssignsAnNK()
    {
        var result = await CreateHandler().HandleAsync(CreateCommand("Nguyễn Kim An"), Admin(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("AnNK", result.Value!.Code);
        var stored = Assert.Single(_repository.Added);
        Assert.Equal("an@example.com", stored.Email);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task CreateThuThuyAfterThuyVtt_AssignsThuyVTT1()
    {
        var handler = CreateHandler();
        var first = await handler.HandleAsync(CreateCommand("Văn Thị Thu Thủy"), Admin(), CancellationToken.None);
        var second = await handler.HandleAsync(CreateCommand("Võ Thị Thu Thúy"), Admin(), CancellationToken.None);

        Assert.Equal("ThuyVTT", first.Value!.Code);
        Assert.Equal("ThuyVTT1", second.Value!.Code);
    }

    [Fact]
    public async Task CreateNameWithoutLetters_DoesNotInsert()
    {
        var result = await CreateHandler().HandleAsync(CreateCommand("---"), Admin(), CancellationToken.None);

        Assert.Equal(EmployeeError.NameUnusable, result.Error);
        Assert.Empty(_repository.Added);
    }

    [Fact]
    public async Task CreateWhenEveryCodeIsTaken_ReturnsCodeExhausted()
    {
        _repository.TreatAllCodesAsExisting = true;

        var result = await CreateHandler().HandleAsync(CreateCommand("Nguyễn Kim An"), Admin(), CancellationToken.None);

        Assert.Equal(EmployeeError.CodeExhausted, result.Error);
    }

    [Fact]
    public async Task CreateWhenUniqueInsertFails_UsesTheNextCode()
    {
        _repository.UniqueRejectionsRemaining = 1;

        var result = await CreateHandler().HandleAsync(CreateCommand("Nguyễn Kim An"), Admin(), CancellationToken.None);

        Assert.Equal("AnNK1", result.Value!.Code);
    }

    [Fact]
    public async Task DirectorCannotCreateAdmin_ButAdminCan()
    {
        var byDirector = await CreateHandler().HandleAsync(CreateCommand("Nguyễn Kim An", EmployeeRole.Admin), Director(), CancellationToken.None);
        var byAdmin = await CreateHandler().HandleAsync(CreateCommand("Trần Văn Bình", EmployeeRole.Admin), Admin(), CancellationToken.None);

        Assert.Equal(EmployeeError.Forbidden, byDirector.Error);
        Assert.True(byAdmin.IsSuccess);
    }

    [Fact]
    public async Task AccountantAndSalesCannotCreateEmployees()
    {
        var accountant = new EmployeeActor(Guid.NewGuid(), EmployeeRole.Accountant, null);
        var sales = new EmployeeActor(Guid.NewGuid(), EmployeeRole.Sales, _kho.Id);

        var one = await CreateHandler().HandleAsync(CreateCommand("Nguyễn Kim An"), accountant, CancellationToken.None);
        var two = await CreateHandler().HandleAsync(CreateCommand("Nguyễn Kim An", EmployeeRole.Sales, _kho.Id), sales, CancellationToken.None);

        Assert.Equal(EmployeeError.Forbidden, one.Error);
        Assert.Equal(EmployeeError.Forbidden, two.Error);
    }

    [Fact]
    public async Task LeadCreatesOnlyKeeperOrSalesInOwnLocation()
    {
        var lead = Lead(_kho.Id);

        var ok = await CreateHandler().HandleAsync(CreateCommand("Nguyễn Kim An", EmployeeRole.WarehouseKeeper, _kho.Id), lead, CancellationToken.None);
        var otherLocation = await CreateHandler().HandleAsync(CreateCommand("Trần Văn Bình", EmployeeRole.Sales, _showroom.Id), lead, CancellationToken.None);
        var wrongRole = await CreateHandler().HandleAsync(CreateCommand("Lê Văn Cường", EmployeeRole.Accountant, _kho.Id), lead, CancellationToken.None);

        Assert.True(ok.IsSuccess);
        Assert.Equal(EmployeeError.Forbidden, otherLocation.Error);
        Assert.Equal(EmployeeError.Forbidden, wrongRole.Error);
    }

    [Fact]
    public async Task KeeperWithoutLocation_IsRejected()
    {
        var result = await CreateHandler().HandleAsync(CreateCommand("Nguyễn Kim An", EmployeeRole.WarehouseKeeper), Admin(), CancellationToken.None);

        Assert.Equal(EmployeeError.LocationRequired, result.Error);
    }

    [Fact]
    public async Task UnknownLocation_IsRejected()
    {
        var result = await CreateHandler().HandleAsync(CreateCommand("Nguyễn Kim An", EmployeeRole.Sales, Guid.NewGuid()), Admin(), CancellationToken.None);

        Assert.Equal(EmployeeError.LocationNotFound, result.Error);
    }

    [Fact]
    public async Task SecondActiveLeadInTheSameLocation_IsRejected()
    {
        Add("Trưởng Một", "TruongM", EmployeeRole.ShowroomWarehouseLead, _kho.Id);

        var result = await CreateHandler().HandleAsync(CreateCommand("Nguyễn Kim An", EmployeeRole.ShowroomWarehouseLead, _kho.Id), Admin(), CancellationToken.None);

        Assert.Equal(EmployeeError.LeadExists, result.Error);
    }

    [Fact]
    public async Task UpdateWithBlankPassword_KeepsTheOldHash()
    {
        var employee = Add("Nguyễn Kim An", "AnNK", EmployeeRole.Accountant);
        var stamp = employee.SecurityStamp;
        var command = UpdateFrom(employee);
        command.FullName = "Nguyễn Kim Bình";
        command.Password = "";

        var result = await UpdateHandler().HandleAsync(command, Admin(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("AnNK", employee.Code);
        Assert.True(_hasher.Verify("Secret12", employee.PasswordHash));
        Assert.Equal("Nguyễn Kim Bình", employee.FullName);
        Assert.Equal(stamp, employee.SecurityStamp);
    }

    [Fact]
    public async Task AdminResetsPassword_RotatesTheStamp()
    {
        var employee = Add("Nguyễn Kim An", "AnNK", EmployeeRole.Accountant);
        var stamp = employee.SecurityStamp;
        var command = UpdateFrom(employee);
        command.Password = "NewSecret1";

        var result = await UpdateHandler().HandleAsync(command, Admin(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(_hasher.Verify("NewSecret1", employee.PasswordHash));
        Assert.NotEqual(stamp, employee.SecurityStamp);
    }

    [Fact]
    public async Task DirectorCannotResetPasswordOrEditPersonalData()
    {
        var employee = Add("Nguyễn Kim An", "AnNK", EmployeeRole.Accountant);
        var reset = UpdateFrom(employee);
        reset.Password = "NewSecret1";
        var rename = UpdateFrom(employee);
        rename.Email = "other@example.com";

        var one = await UpdateHandler().HandleAsync(reset, Director(), CancellationToken.None);
        var two = await UpdateHandler().HandleAsync(rename, Director(), CancellationToken.None);

        Assert.Equal(EmployeeError.Forbidden, one.Error);
        Assert.Equal(EmployeeError.Forbidden, two.Error);
        Assert.True(_hasher.Verify("Secret12", employee.PasswordHash));
        Assert.Equal("x@example.com", employee.Email);
    }

    [Fact]
    public async Task DirectorChangesRoleTitleAndLocation()
    {
        var employee = Add("Nguyễn Kim An", "AnNK", EmployeeRole.Sales, _kho.Id);
        var command = UpdateFrom(employee);
        command.Role = EmployeeRole.WarehouseKeeper;
        command.JobTitle = "Thủ kho";
        command.StockLocationId = _showroom.Id;

        var result = await UpdateHandler().HandleAsync(command, Director(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(EmployeeRole.WarehouseKeeper, employee.Role);
        Assert.Equal(_showroom.Id, employee.StockLocationId);
    }

    [Fact]
    public async Task DirectorCannotTouchAdminOrGrantAdmin()
    {
        var admin = Add("Quản Trị", "admin", EmployeeRole.Admin);
        var accountant = Add("Nguyễn Kim An", "AnNK", EmployeeRole.Accountant);
        var demote = UpdateFrom(admin);
        demote.Role = EmployeeRole.Sales;
        var promote = UpdateFrom(accountant);
        promote.Role = EmployeeRole.Admin;

        var one = await UpdateHandler().HandleAsync(demote, Director(), CancellationToken.None);
        var two = await UpdateHandler().HandleAsync(promote, Director(), CancellationToken.None);

        Assert.Equal(EmployeeError.Forbidden, one.Error);
        Assert.Equal(EmployeeError.Forbidden, two.Error);
        Assert.Equal(EmployeeRole.Admin, admin.Role);
        Assert.Equal(EmployeeRole.Accountant, accountant.Role);
    }

    [Fact]
    public async Task LastAdminCannotBeDemoted()
    {
        var onlyAdmin = Add("Quản Trị", "admin", EmployeeRole.Admin);
        var command = UpdateFrom(onlyAdmin);
        command.Role = EmployeeRole.Accountant;

        var result = await UpdateHandler().HandleAsync(command, Admin(), CancellationToken.None);

        Assert.Equal(EmployeeError.LastAdmin, result.Error);
        Assert.Equal(EmployeeRole.Admin, onlyAdmin.Role);
    }

    [Fact]
    public async Task NobodyChangesTheirOwnAssignment()
    {
        var admin = Add("Quản Trị", "admin", EmployeeRole.Admin);
        Add("Quản Trị Hai", "admin2", EmployeeRole.Admin);
        var command = UpdateFrom(admin);
        command.Role = EmployeeRole.Accountant;

        var result = await UpdateHandler().HandleAsync(command, new EmployeeActor(admin.Id, admin.Role, null), CancellationToken.None);

        Assert.Equal(EmployeeError.Forbidden, result.Error);
    }

    [Fact]
    public async Task LeadCannotEditAnyone()
    {
        var keeper = Add("Thủ Kho", "ThuK", EmployeeRole.WarehouseKeeper, _kho.Id);
        var command = UpdateFrom(keeper);
        command.JobTitle = "Khác";

        var result = await UpdateHandler().HandleAsync(command, Lead(_kho.Id), CancellationToken.None);

        Assert.Equal(EmployeeError.Forbidden, result.Error);
    }

    [Fact]
    public async Task LeadCannotViewEmployeeOfAnotherLocation()
    {
        var other = Add("Sales B", "SalesB", EmployeeRole.Sales, _showroom.Id);
        var handler = new GetEmployeeHandler(_repository, NullLogger<GetEmployeeHandler>.Instance);

        var result = await handler.HandleAsync(other.Id, Lead(_kho.Id), CancellationToken.None);

        Assert.Equal(EmployeeError.Forbidden, result.Error);
    }

    [Fact]
    public async Task ListIsScopedForLeadAndClosedForOthers()
    {
        Add("Thủ Kho A", "ThuKA", EmployeeRole.WarehouseKeeper, _kho.Id);
        Add("Sales B", "SalesB", EmployeeRole.Sales, _showroom.Id);
        var handler = new ListEmployeesHandler(_repository, _locations, NullLogger<ListEmployeesHandler>.Instance);

        var all = await handler.HandleAsync(new EmployeeActor(Guid.NewGuid(), EmployeeRole.Accountant, null), null, null, null, CancellationToken.None);
        var own = await handler.HandleAsync(Lead(_kho.Id), null, null, _showroom.Id, CancellationToken.None);
        var sales = await handler.HandleAsync(new EmployeeActor(Guid.NewGuid(), EmployeeRole.Sales, _kho.Id), null, null, null, CancellationToken.None);

        Assert.Equal(2, all.Count);
        Assert.Equal("ThuKA", Assert.Single(own).Code);
        Assert.Empty(sales);
    }

    [Fact]
    public async Task DeactivateRotatesStamp_AndLastAdminIsProtected()
    {
        var onlyAdmin = Add("Quản Trị", "admin", EmployeeRole.Admin);
        var accountant = Add("Nguyễn Kim An", "AnNK", EmployeeRole.Accountant);
        var stamp = accountant.SecurityStamp;
        var handler = new SetEmployeeActiveCommandHandler(_repository, NullLogger<SetEmployeeActiveCommandHandler>.Instance);

        var done = await handler.HandleAsync(accountant.Id, false, Director(), CancellationToken.None);
        var lastAdmin = await handler.HandleAsync(onlyAdmin.Id, false, Admin(), CancellationToken.None);
        var byDirector = await handler.HandleAsync(onlyAdmin.Id, false, Director(), CancellationToken.None);

        Assert.True(done.IsSuccess);
        Assert.False(accountant.IsActive);
        Assert.NotEqual(stamp, accountant.SecurityStamp);
        Assert.Equal(EmployeeError.LastAdmin, lastAdmin.Error);
        Assert.Equal(EmployeeError.Forbidden, byDirector.Error);
        Assert.True(onlyAdmin.IsActive);
    }

    [Fact]
    public async Task OwnProfile_UpdatesContactOnly()
    {
        var employee = Add("Nguyễn Kim An", "AnNK", EmployeeRole.Sales, _kho.Id);
        var handler = new UpdateOwnProfileCommandHandler(_repository, NullLogger<UpdateOwnProfileCommandHandler>.Instance);

        var result = await handler.HandleAsync(
            new UpdateOwnProfileCommand { FullName = "Nguyễn Kim An B", Email = "new@example.com", Phone = "0912345678", Address = "Hà Nội" },
            new EmployeeActor(employee.Id, employee.Role, employee.StockLocationId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hà Nội", employee.Address);
        Assert.Equal(EmployeeRole.Sales, employee.Role);
        Assert.Equal(_kho.Id, employee.StockLocationId);
        Assert.Equal("AnNK", employee.Code);
    }

    [Fact]
    public async Task OwnPassword_NeedsTheCurrentOneAndMinimumLength()
    {
        var employee = Add("Nguyễn Kim An", "AnNK", EmployeeRole.Sales, _kho.Id);
        var handler = new ChangeOwnPasswordCommandHandler(_repository, _hasher, NullLogger<ChangeOwnPasswordCommandHandler>.Instance);
        var actor = new EmployeeActor(employee.Id, employee.Role, employee.StockLocationId);

        var wrong = await handler.HandleAsync(new ChangeOwnPasswordCommand { CurrentPassword = "nope", NewPassword = "Another123" }, actor, CancellationToken.None);
        var weak = await handler.HandleAsync(new ChangeOwnPasswordCommand { CurrentPassword = "Secret12", NewPassword = "short" }, actor, CancellationToken.None);
        var ok = await handler.HandleAsync(new ChangeOwnPasswordCommand { CurrentPassword = "Secret12", NewPassword = "Another123" }, actor, CancellationToken.None);

        Assert.Equal(EmployeeError.WrongPassword, wrong.Error);
        Assert.Equal(EmployeeError.WeakPassword, weak.Error);
        Assert.True(ok.IsSuccess);
        Assert.True(_hasher.Verify("Another123", employee.PasswordHash));
    }

    [Fact]
    public async Task InactiveEmployee_CannotLogIn()
    {
        var employee = Add("Nguyễn Kim An", "AnNK", EmployeeRole.Accountant);
        employee.Deactivate();
        var handler = new LoginEmployeeCommandHandler(_repository, _hasher, NullLogger<LoginEmployeeCommandHandler>.Instance);

        var result = await handler.HandleAsync(new LoginEmployeeCommand { EmployeeCode = "AnNK", Password = "Secret12" }, CancellationToken.None);

        Assert.Equal(LoginError.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task BlankRole_AsksForARole_AndDoesNotCreateAnyone()
    {
        var before = _repository.Employees.Count;

        var blank = await CreateHandler().HandleAsync(CreateCommand("Nguyễn Kim An", " "), Admin(), CancellationToken.None);
        var unknown = await CreateHandler().HandleAsync(CreateCommand("Nguyễn Kim An", "Nope"), Admin(), CancellationToken.None);

        Assert.Equal(EmployeeError.RoleRequired, blank.Error);
        Assert.Equal(EmployeeError.Forbidden, unknown.Error);
        Assert.Equal(before, _repository.Employees.Count);
    }

    [Fact]
    public async Task DeactivateAndRoleChange_UseTheirOwnAuditActions()
    {
        var audit = new MemoryAudit();
        var admin = Add("Quản Trị", "admin", EmployeeRole.Admin);
        Add("Quản Trị Hai", "admin2", EmployeeRole.Admin);
        var accountant = Add("Nguyễn Kim An", "AnNK", EmployeeRole.Accountant);
        var active = new SetEmployeeActiveCommandHandler(
            _repository, NullLogger<SetEmployeeActiveCommandHandler>.Instance, audit: audit);
        var update = new UpdateEmployeeCommandHandler(
            _repository, _locations, _hasher, NullLogger<UpdateEmployeeCommandHandler>.Instance, audit: audit);

        var deactivated = await active.HandleAsync(accountant.Id, false, new EmployeeActor(admin.Id, admin.Role, null), CancellationToken.None);
        var command = UpdateFrom(accountant);
        command.Role = EmployeeRole.Sales;
        command.StockLocationId = _kho.Id;
        var changed = await update.HandleAsync(command, new EmployeeActor(admin.Id, admin.Role, null), CancellationToken.None);
        var contact = UpdateFrom(accountant);
        contact.Address = "Hà Nội";
        var edited = await update.HandleAsync(contact, Admin(), CancellationToken.None);

        Assert.True(deactivated.IsSuccess);
        Assert.True(changed.IsSuccess);
        Assert.True(edited.IsSuccess);
        Assert.Contains(audit.Entries, entry => entry.Action == AuditActions.EmployeeDeactivated);
        Assert.Contains(audit.Entries, entry => entry.Action == AuditActions.EmployeeRoleChanged);
        Assert.Contains(audit.Entries, entry => entry.Action == AuditActions.EmployeeUpdated);
        Assert.DoesNotContain(audit.Entries, entry => entry.Action == AuditActions.EmployeeActivated && entry.Detail == "deactivated");
    }

    [Fact]
    public async Task ConcurrentAdminDeactivation_LeavesOneActiveAdmin()
    {
        var first = Add("Quản Trị Một", "admin1", EmployeeRole.Admin);
        var second = Add("Quản Trị Hai", "admin2", EmployeeRole.Admin);
        var counted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _repository.GateFirstAdminCount(counted, release);
        var handler = new SetEmployeeActiveCommandHandler(_repository, NullLogger<SetEmployeeActiveCommandHandler>.Instance);

        var stoppingSecond = handler.HandleAsync(second.Id, false, new EmployeeActor(first.Id, first.Role, null), CancellationToken.None);
        await counted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, _repository.AdminCounts);
        var stoppingFirst = handler.HandleAsync(first.Id, false, new EmployeeActor(second.Id, second.Role, null), CancellationToken.None);
        await Task.Delay(50);
        Assert.Equal(1, _repository.AdminCounts);
        release.TrySetResult(true);
        var results = await Task.WhenAll(stoppingSecond, stoppingFirst);

        Assert.Equal(1, results.Count(result => result.IsSuccess));
        Assert.Contains(results, result => result.Error == EmployeeError.LastAdmin);
        Assert.Equal(1, _repository.Employees.Count(employee => employee.Role == EmployeeRole.Admin && employee.IsActive));
    }
}
