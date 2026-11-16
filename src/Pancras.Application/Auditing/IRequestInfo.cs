using Pancras.Domain.Auditing;

namespace Pancras.Application.Auditing;

/// <summary>Thông tin yêu cầu hiện tại, do lớp Web cung cấp.</summary>
public interface IRequestInfo
{
    string? IpAddress { get; }

    Guid? ActorId { get; }

    string? ActorCode { get; }
}
