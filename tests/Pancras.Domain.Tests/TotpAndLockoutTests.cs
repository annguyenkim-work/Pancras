using System.Text;
using Pancras.Domain.Employees;

namespace Pancras.Domain.Tests;

public class TotpAndLockoutTests
{
    private static readonly byte[] Rfc6238Secret = Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    public void Generate_MatchesRfc6238Vectors(long unixSeconds, string expected)
    {
        var step = Totp.StepFor(DateTimeOffset.FromUnixTimeSeconds(unixSeconds));

        Assert.Equal(expected, Totp.Generate(Rfc6238Secret, step));
    }

    [Fact]
    public void Base32_RoundTrips()
    {
        var secret = Totp.NewSecret();

        var text = Totp.ToBase32(secret);

        Assert.Equal(secret, Totp.FromBase32(text));
        Assert.Equal(secret, Totp.FromBase32(text.ToLowerInvariant()));
        Assert.Null(Totp.FromBase32("not*base32"));
    }

    [Fact]
    public void Verify_AcceptsOneStepOfDrift_AndRejectsReplay()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var step = Totp.StepFor(now);
        var previous = Totp.Generate(Rfc6238Secret, step - 1);
        var farAway = Totp.Generate(Rfc6238Secret, step + 5);

        var accepted = Totp.Verify(Rfc6238Secret, previous, now, 0);
        var replay = Totp.Verify(Rfc6238Secret, previous, now, step - 1);
        var rejected = Totp.Verify(Rfc6238Secret, farAway, now, 0);
        var wrongLength = Totp.Verify(Rfc6238Secret, "123", now, 0);

        Assert.Equal(step - 1, accepted);
        Assert.Null(replay);
        Assert.Null(rejected);
        Assert.Null(wrongLength);
    }

    [Fact]
    public void FiveFailures_LockForFifteenMinutes_AndSuccessResets()
    {
        var employee = Employee.Create("Nguyễn Kim An", "AnNK", "ANNK", "hash", EmployeeRole.Sales);
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

        for (var index = 0; index < 4; index++)
        {
            Assert.False(employee.RegisterFailedLogin(now));
        }

        Assert.False(employee.IsLockedOut(now));
        Assert.True(employee.RegisterFailedLogin(now));
        Assert.True(employee.IsLockedOut(now.AddMinutes(14)));
        Assert.False(employee.IsLockedOut(now.AddMinutes(16)));

        employee.RegisterSuccessfulLogin();

        Assert.False(employee.IsLockedOut(now));
        Assert.Equal(0, employee.FailedLoginCount);
    }

    [Fact]
    public void RecoveryCodes_WorkOnce_AndAreStoredHashed()
    {
        var employee = Employee.Create("Nguyễn Kim An", "AnNK", "ANNK", "hash", EmployeeRole.Sales);
        var codes = RecoveryCodes.Generate();
        employee.EnableTwoFactor("protected", codes.Select(RecoveryCodes.Hash), 7);

        Assert.Equal(RecoveryCodes.Count, employee.RecoveryCodesLeft);
        Assert.DoesNotContain(codes[0], employee.RecoveryCodeHashes, StringComparison.Ordinal);
        Assert.True(employee.TryUseRecoveryCode(codes[0].ToLowerInvariant()));
        Assert.False(employee.TryUseRecoveryCode(codes[0]));
        Assert.Equal(RecoveryCodes.Count - 1, employee.RecoveryCodesLeft);
    }

    [Fact]
    public void EnablingAndDisablingTwoFactor_RotatesTheStamp()
    {
        var employee = Employee.Create("Nguyễn Kim An", "AnNK", "ANNK", "hash", EmployeeRole.Sales);
        var stamp = employee.SecurityStamp;

        employee.EnableTwoFactor("protected", [], 1);
        var enabledStamp = employee.SecurityStamp;
        employee.DisableTwoFactor();

        Assert.NotEqual(stamp, enabledStamp);
        Assert.NotEqual(enabledStamp, employee.SecurityStamp);
        Assert.False(employee.TwoFactorEnabled);
        Assert.Equal(string.Empty, employee.TotpSecret);
    }
}
