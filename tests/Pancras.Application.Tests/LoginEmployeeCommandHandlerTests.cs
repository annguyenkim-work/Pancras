using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Employees;

namespace Pancras.Application.Tests;

public class LoginEmployeeCommandHandlerTests
{
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeEmployeeRepository _repository = new();
    private readonly LoginEmployeeCommandHandler _handler;

    public LoginEmployeeCommandHandlerTests()
    {
        var admin = Employee.Create(
            "Quản trị hệ thống",
            "admin",
            "ADMIN",
            _hasher.Hash("Admin@12345"),
            EmployeeRole.Admin);
        _repository.Employees.Add(admin);
        _handler = new LoginEmployeeCommandHandler(
            _repository,
            _hasher,
            NullLogger<LoginEmployeeCommandHandler>.Instance);
    }

    [Fact]
    public async Task AdminPassword_SignsInAsAdmin()
    {
        var result = await _handler.HandleAsync(
            new LoginEmployeeCommand { EmployeeCode = "admin", Password = "Admin@12345" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Admin", result.Value!.Role);
        Assert.Equal("Quản trị hệ thống", result.Value.FullName);
        Assert.Equal(_repository.Employees[0].SecurityStamp, result.Value.SecurityStamp);
    }

    [Fact]
    public async Task WrongPassword_IsInvalid()
    {
        var result = await _handler.HandleAsync(
            new LoginEmployeeCommand { EmployeeCode = "admin", Password = "wrong" },
            CancellationToken.None);

        Assert.Equal(LoginError.InvalidCredentials, result.LoginError);
    }

    [Fact]
    public async Task UnknownCode_IsInvalid()
    {
        var result = await _handler.HandleAsync(
            new LoginEmployeeCommand { EmployeeCode = "missing", Password = "Admin@12345" },
            CancellationToken.None);

        Assert.Equal(LoginError.InvalidCredentials, result.LoginError);
    }

    [Fact]
    public async Task EmptyFields_AreMissing()
    {
        var missingPassword = await _handler.HandleAsync(
            new LoginEmployeeCommand { EmployeeCode = "admin", Password = "" },
            CancellationToken.None);
        var missingCode = await _handler.HandleAsync(
            new LoginEmployeeCommand { EmployeeCode = "   ", Password = "Admin@12345" },
            CancellationToken.None);

        Assert.Equal(LoginError.MissingCredentials, missingPassword.LoginError);
        Assert.Equal(LoginError.MissingCredentials, missingCode.LoginError);
    }

    [Fact]
    public async Task CodeMatch_IgnoresCaseAndOuterSpaces()
    {
        var result = await _handler.HandleAsync(
            new LoginEmployeeCommand { EmployeeCode = "  Admin  ", Password = "Admin@12345" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("admin", result.Value!.Code);
    }

    [Fact]
    public async Task Password_IsNotTrimmed()
    {
        var spaced = await _handler.HandleAsync(
            new LoginEmployeeCommand { EmployeeCode = "admin", Password = " Admin@12345" },
            CancellationToken.None);

        Assert.Equal(LoginError.InvalidCredentials, spaced.LoginError);
    }
}
