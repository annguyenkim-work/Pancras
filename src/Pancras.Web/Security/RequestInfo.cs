using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Pancras.Application.Auditing;
using Pancras.Application.Employees;

namespace Pancras.Web.Security;

public sealed class RequestInfo(IHttpContextAccessor accessor) : IRequestInfo
{
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public Guid? ActorId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? ActorCode => accessor.HttpContext?.User.FindFirstValue(EmployeeSessionClaims.EmployeeCode);
}

public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Pancras.TwoFactor.Secret.v1");

    public string Protect(string plain) => _protector.Protect(plain);

    public string? Unprotect(string protectedText)
    {
        try
        {
            return _protector.Unprotect(protectedText);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}

/// <summary>Cookie ngắn hạn giữa bước mật khẩu và bước mã 2FA. Không cấp quyền truy cập.</summary>
public static class PendingTwoFactor
{
    public const string Scheme = "Pancras.2FA";

    public const string CookieName = "Pancras.2FA";
}
