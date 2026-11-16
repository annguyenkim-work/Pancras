using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Auditing;
using Pancras.Application.Common;
using Pancras.Application.Employees;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;

namespace Pancras.Application.Tests;

internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan span) => _now += span;
}

internal sealed class PlainProtector : ISecretProtector
{
    public string Protect(string plain) => "p:" + plain;

    public string? Unprotect(string protectedText) => protectedText.StartsWith("p:", StringComparison.Ordinal) ? protectedText[2..] : null;
}

internal sealed class MemoryAudit : IAuditLog
{
    public List<AuditEntry> Entries { get; } = [];

    public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntry>> ListAsync(string? search, int pageNumber, int pageSize, bool newestFirst, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AuditEntry>>(Entries);

    public Task<int> CountAsync(string? search, CancellationToken cancellationToken) => Task.FromResult(Entries.Count);
}

public class LoginLockoutAndTwoFactorTests
{
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeEmployeeRepository _repository = new();
    private readonly TestClock _clock = new(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));
    private readonly MemoryAudit _audit = new();
    private readonly PlainProtector _protector = new();
    private readonly Employee _employee;

    public LoginLockoutAndTwoFactorTests()
    {
        _employee = Employee.Create("Nguyễn Kim An", "AnNK", "ANNK", _hasher.Hash("Secret12"), EmployeeRole.Accountant);
        _repository.Employees.Add(_employee);
    }

    private LoginEmployeeCommandHandler Login() =>
        new(_repository, _hasher, NullLogger<LoginEmployeeCommandHandler>.Instance, _clock, _audit);

    private VerifyTwoFactorCommandHandler Verify() =>
        new(_repository, _protector, NullLogger<VerifyTwoFactorCommandHandler>.Instance, _clock, _audit);

    private Task<Result<EmployeeSession>> Attempt(string password) =>
        Login().HandleAsync(new LoginEmployeeCommand { EmployeeCode = "AnNK", Password = password }, CancellationToken.None);

    [Fact]
    public async Task FiveWrongPasswords_LockTheAccount_EvenForTheRightPassword()
    {
        for (var index = 0; index < 5; index++)
        {
            Assert.Equal(LoginError.InvalidCredentials, (await Attempt("wrong")).LoginError);
        }

        var locked = await Attempt("Secret12");

        Assert.Equal(LoginError.InvalidCredentials, locked.LoginError);
        Assert.Contains(_audit.Entries, entry => entry.Action == AuditActions.LoginLockedOut);
    }

    [Fact]
    public async Task TheLockEndsAfterFifteenMinutes()
    {
        for (var index = 0; index < 5; index++)
        {
            await Attempt("wrong");
        }

        _clock.Advance(TimeSpan.FromMinutes(16));
        var result = await Attempt("Secret12");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _employee.FailedLoginCount);
    }

    [Fact]
    public async Task ASuccessfulLoginClearsEarlierFailures()
    {
        for (var index = 0; index < 4; index++)
        {
            await Attempt("wrong");
        }

        var ok = await Attempt("Secret12");
        for (var index = 0; index < 4; index++)
        {
            await Attempt("wrong");
        }

        Assert.True(ok.IsSuccess);
        Assert.False(_employee.IsLockedOut(_clock.GetUtcNow()));
    }

    [Fact]
    public async Task UnknownAccountsAndInactiveOnesFailTheSameWay()
    {
        var unknown = await Login().HandleAsync(new LoginEmployeeCommand { EmployeeCode = "Nobody", Password = "Secret12" }, CancellationToken.None);
        _employee.Deactivate();
        var inactive = await Attempt("Secret12");

        Assert.Equal(LoginError.InvalidCredentials, unknown.LoginError);
        Assert.Equal(LoginError.InvalidCredentials, inactive.LoginError);
    }

    [Fact]
    public async Task SuccessfulLogin_IsAudited_WithoutThePassword()
    {
        await Attempt("Secret12");

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditActions.LoginSucceeded, entry.Action);
        Assert.Equal(_employee.Id, entry.ActorEmployeeId);
        Assert.DoesNotContain("Secret12", entry.Detail, StringComparison.Ordinal);
    }

    private async Task<(string Secret, IReadOnlyList<string> Codes)> EnableAsync()
    {
        var begin = await new BeginTwoFactorSetupHandler(_repository, _protector).HandleAsync(Actor(), CancellationToken.None);
        var secret = Totp.FromBase32(begin.Value!.Secret)!;
        var code = Totp.Generate(secret, Totp.StepFor(_clock.GetUtcNow()));
        var confirm = new ConfirmTwoFactorSetupCommandHandler(_repository, _protector, NullLogger<ConfirmTwoFactorSetupCommandHandler>.Instance, _clock, _audit);
        var result = await confirm.HandleAsync(
            new ConfirmTwoFactorCommand { ProtectedSecret = begin.Value.ProtectedSecret, Code = code },
            Actor(),
            CancellationToken.None);
        Assert.True(result.IsSuccess);
        return (begin.Value.Secret, result.Value!.RecoveryCodes);
    }

    private EmployeeActor Actor() => new(_employee.Id, _employee.Role, null);

    [Fact]
    public async Task Setup_RejectsAWrongCode_AndEnablesWithARightOne()
    {
        var begin = await new BeginTwoFactorSetupHandler(_repository, _protector).HandleAsync(Actor(), CancellationToken.None);
        var confirm = new ConfirmTwoFactorSetupCommandHandler(_repository, _protector, NullLogger<ConfirmTwoFactorSetupCommandHandler>.Instance, _clock, _audit);

        var wrong = await confirm.HandleAsync(
            new ConfirmTwoFactorCommand { ProtectedSecret = begin.Value!.ProtectedSecret, Code = "000000" },
            Actor(),
            CancellationToken.None);

        Assert.Equal(EmployeeError.InvalidCode, wrong.EmployeeError);
        Assert.False(_employee.TwoFactorEnabled);

        var (_, codes) = await EnableAsync();

        Assert.True(_employee.TwoFactorEnabled);
        Assert.Equal(RecoveryCodes.Count, codes.Count);
        Assert.Contains(_audit.Entries, entry => entry.Action == AuditActions.TwoFactorEnabled);
    }

    [Fact]
    public async Task WithTwoFactor_APasswordAloneIsNotEnough()
    {
        await EnableAsync();

        var result = await Attempt("Secret12");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RequiresTwoFactor);
        Assert.DoesNotContain(_audit.Entries, entry => entry.Action == AuditActions.LoginSucceeded);
    }

    [Fact]
    public async Task ACodeWorksOnce_AndAWrongCodeCountsTowardsTheLock()
    {
        var (secret, _) = await EnableAsync();
        _clock.Advance(TimeSpan.FromSeconds(31));
        var code = Totp.Generate(Totp.FromBase32(secret)!, Totp.StepFor(_clock.GetUtcNow()));

        var first = await Verify().HandleAsync(new VerifyTwoFactorCommand { EmployeeId = _employee.Id, Code = code }, CancellationToken.None);
        var replay = await Verify().HandleAsync(new VerifyTwoFactorCommand { EmployeeId = _employee.Id, Code = code }, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(LoginError.InvalidCode, replay.LoginError);
        Assert.Equal(1, _employee.FailedLoginCount);
        Assert.Contains(_audit.Entries, entry => entry.Action == AuditActions.TwoFactorFailed);
    }

    [Fact]
    public async Task ARecoveryCodeLogsInOnce()
    {
        var (_, codes) = await EnableAsync();

        var first = await Verify().HandleAsync(new VerifyTwoFactorCommand { EmployeeId = _employee.Id, Code = codes[0] }, CancellationToken.None);
        var second = await Verify().HandleAsync(new VerifyTwoFactorCommand { EmployeeId = _employee.Id, Code = codes[0] }, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(LoginError.InvalidCode, second.LoginError);
        Assert.Contains(_audit.Entries, entry => entry.Action == AuditActions.RecoveryCodeUsed);
    }

    [Fact]
    public async Task DisablingNeedsThePasswordAndACode_AndOnlyAdminCanResetForOthers()
    {
        var (secret, codes) = await EnableAsync();
        var disable = new DisableTwoFactorCommandHandler(_repository, _hasher, _protector, NullLogger<DisableTwoFactorCommandHandler>.Instance, _clock, _audit);

        var noPassword = await disable.HandleAsync(new DisableTwoFactorCommand { CurrentPassword = "bad", Code = codes[0] }, Actor(), CancellationToken.None);
        var noCode = await disable.HandleAsync(new DisableTwoFactorCommand { CurrentPassword = "Secret12", Code = "111111" }, Actor(), CancellationToken.None);
        var reset = new ResetTwoFactorCommandHandler(_repository, NullLogger<ResetTwoFactorCommandHandler>.Instance, _clock, _audit);
        var byDirector = await reset.HandleAsync(_employee.Id, new EmployeeActor(Guid.NewGuid(), EmployeeRole.Director, null), CancellationToken.None);
        var byAdmin = await reset.HandleAsync(_employee.Id, new EmployeeActor(Guid.NewGuid(), EmployeeRole.Admin, null), CancellationToken.None);

        Assert.Equal(EmployeeError.WrongPassword, noPassword.EmployeeError);
        Assert.Equal(EmployeeError.InvalidCode, noCode.EmployeeError);
        Assert.Equal(EmployeeError.Forbidden, byDirector.EmployeeError);
        Assert.True(byAdmin.IsSuccess);
        Assert.False(_employee.TwoFactorEnabled);
        Assert.NotNull(secret);
    }

    [Fact]
    public async Task ReactivatingAnEmployee_KeepsTwoFactorAndTheLock_SoOnlyAdminCanClearThem()
    {
        var (secret, _) = await EnableAsync();
        for (var index = 0; index < 5; index++)
        {
            await Attempt("wrong");
        }

        var stamp = _employee.SecurityStamp;
        _employee.Deactivate();
        _employee.Activate();
        var whileLocked = await Attempt("Secret12");
        _clock.Advance(TimeSpan.FromMinutes(16));
        var afterLock = await Attempt("Secret12");
        var code = Totp.Generate(Totp.FromBase32(secret)!, Totp.StepFor(_clock.GetUtcNow()));
        var verified = await Verify().HandleAsync(new VerifyTwoFactorCommand { EmployeeId = _employee.Id, Code = code }, CancellationToken.None);

        Assert.True(_employee.IsActive);
        Assert.True(_employee.TwoFactorEnabled);
        Assert.NotEqual(string.Empty, _employee.TotpSecret);
        Assert.NotEqual(stamp, _employee.SecurityStamp);
        Assert.Equal(LoginError.InvalidCredentials, whileLocked.LoginError);
        Assert.True(afterLock.Value!.RequiresTwoFactor);
        Assert.True(verified.IsSuccess);
    }

    [Fact]
    public async Task DeactivateThenActivate_DoesNotConsumeTheRecoveryCodes()
    {
        await EnableAsync();
        _employee.Deactivate();
        _employee.Activate();

        Assert.True(_employee.TwoFactorEnabled);
        Assert.Equal(RecoveryCodes.Count, _employee.RecoveryCodesLeft);
    }

    [Fact]
    public async Task OnlyAdminUnlocks()
    {
        for (var index = 0; index < 5; index++)
        {
            await Attempt("wrong");
        }

        var unlock = new UnlockEmployeeCommandHandler(_repository, NullLogger<UnlockEmployeeCommandHandler>.Instance, _clock, _audit);
        var byAccountant = await unlock.HandleAsync(_employee.Id, new EmployeeActor(Guid.NewGuid(), EmployeeRole.Accountant, null), CancellationToken.None);
        var byAdmin = await unlock.HandleAsync(_employee.Id, new EmployeeActor(Guid.NewGuid(), EmployeeRole.Admin, null), CancellationToken.None);
        var login = await Attempt("Secret12");

        Assert.Equal(EmployeeError.Forbidden, byAccountant.EmployeeError);
        Assert.True(byAdmin.IsSuccess);
        Assert.True(login.IsSuccess);
    }
}
