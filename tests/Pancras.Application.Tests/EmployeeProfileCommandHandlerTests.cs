using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Employees;

namespace Pancras.Application.Tests;

public class EmployeeProfileCommandHandlerTests
{
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeEmployeeRepository _repository = new();

    [Fact]
    public async Task CreateWithoutPassword_DoesNotInsert()
    {
        var handler = new CreateEmployeeCommandHandler(
            _repository,
            _hasher,
            NullLogger<CreateEmployeeCommandHandler>.Instance);

        var result = await handler.HandleAsync(
            new CreateEmployeeCommand { FullName = "Nguyễn Kim An", Password = "", Role = EmployeeRole.Admin },
            CancellationToken.None);

        Assert.Equal(EmployeeError.ProfileIncomplete, result.EmployeeError);
        Assert.Empty(_repository.Added);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task UpdateWithBlankPassword_KeepsTheOldHash()
    {
        var employee = Employee.Create(
            "Nguyễn Kim An",
            "AnNK",
            "ANNK",
            _hasher.Hash("Secret1"),
            EmployeeRole.Sales);
        _repository.Employees.Add(employee);
        var stamp = employee.SecurityStamp;
        var handler = new UpdateEmployeeCommandHandler(
            _repository,
            _hasher,
            NullLogger<UpdateEmployeeCommandHandler>.Instance);

        var result = await handler.HandleAsync(
            new UpdateEmployeeCommand
            {
                Id = employee.Id,
                FullName = "Nguyễn Kim Bình",
                Password = "",
                Role = EmployeeRole.Sales,
                Email = "binh@example.com",
                Phone = "0901000001",
                Branch = "Showroom 1",
                JobTitle = "Bán hàng",
                Department = "Kinh doanh"
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("AnNK", employee.Code);
        Assert.True(_hasher.Verify("Secret1", employee.PasswordHash));
        Assert.Equal("Nguyễn Kim Bình", employee.FullName);
        Assert.Equal("Showroom 1", employee.Branch);
        Assert.Equal(stamp, employee.SecurityStamp);
    }

    [Fact]
    public async Task UpdateWithNewPassword_ReplacesTheHash()
    {
        var employee = Employee.Create(
            "Nguyễn Kim An",
            "AnNK",
            "ANNK",
            _hasher.Hash("Secret1"),
            EmployeeRole.Admin);
        _repository.Employees.Add(employee);
        var stamp = employee.SecurityStamp;
        var handler = new UpdateEmployeeCommandHandler(
            _repository,
            _hasher,
            NullLogger<UpdateEmployeeCommandHandler>.Instance);

        var result = await handler.HandleAsync(
            new UpdateEmployeeCommand
            {
                Id = employee.Id,
                FullName = "Nguyễn Kim An",
                Password = "Secret2",
                Role = EmployeeRole.Sales,
                Email = "an@example.com",
                Phone = "0901000000",
                Branch = "Kho A",
                JobTitle = "Thủ kho"
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("AnNK", employee.Code);
        Assert.Equal(EmployeeRole.Sales, employee.Role);
        Assert.True(_hasher.Verify("Secret2", employee.PasswordHash));
        Assert.False(_hasher.Verify("Secret1", employee.PasswordHash));
        Assert.NotEqual(stamp, employee.SecurityStamp);
    }

    [Fact]
    public async Task CreateNguyenKimAn_AssignsAnNK()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(CreateCommand("Nguyễn Kim An"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("AnNK", result.Value!.Code);
        var stored = Assert.Single(_repository.Added);
        Assert.Equal("an@example.com", stored.Email);
        Assert.Equal("0901000000", stored.Phone);
        Assert.Equal("Kho A", stored.Branch);
        Assert.Equal("Thủ kho", stored.JobTitle);
    }

    [Fact]
    public async Task CreateThuThuyAfterThuyVtt_AssignsThuyVTT1()
    {
        var handler = CreateHandler();
        var first = await handler.HandleAsync(CreateCommand("Văn Thị Thu Thủy"), CancellationToken.None);
        var second = await handler.HandleAsync(CreateCommand("Võ Thị Thu Thúy"), CancellationToken.None);

        Assert.Equal("ThuyVTT", first.Value!.Code);
        Assert.Equal("ThuyVTT1", second.Value!.Code);
    }

    [Fact]
    public async Task CreateNameWithoutLetters_DoesNotInsert()
    {
        var handler = CreateHandler();

        var result = await handler.HandleAsync(CreateCommand("---"), CancellationToken.None);

        Assert.Equal(EmployeeError.NameUnusable, result.EmployeeError);
        Assert.Empty(_repository.Added);
    }

    [Fact]
    public async Task CreateWhenEveryCodeIsTaken_ReturnsCodeExhausted()
    {
        _repository.TreatAllCodesAsExisting = true;
        var handler = CreateHandler();

        var result = await handler.HandleAsync(CreateCommand("Nguyễn Kim An"), CancellationToken.None);

        Assert.Equal(EmployeeError.CodeExhausted, result.EmployeeError);
        Assert.Empty(_repository.Added);
    }

    [Fact]
    public async Task CreateWhenUniqueInsertFails_UsesTheNextCode()
    {
        _repository.UniqueRejectionsRemaining = 1;
        var handler = CreateHandler();

        var result = await handler.HandleAsync(CreateCommand("Nguyễn Kim An"), CancellationToken.None);

        Assert.Equal("AnNK1", result.Value!.Code);
        Assert.Single(_repository.Added);
    }

    private CreateEmployeeCommandHandler CreateHandler() =>
        new(_repository, _hasher, NullLogger<CreateEmployeeCommandHandler>.Instance);

    private static CreateEmployeeCommand CreateCommand(string fullName) =>
        new()
        {
            FullName = fullName,
            Password = "Secret1",
            Role = EmployeeRole.Sales,
            Email = "an@example.com",
            Phone = "0901000000",
            Branch = "Kho A",
            JobTitle = "Thủ kho",
            Department = "Kho"
        };
}
