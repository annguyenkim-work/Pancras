namespace Pancras.Application.Auditing;

/// <summary>
/// Bộ lọc danh sách nhật ký. <c>From</c>/<c>To</c> là ngày theo giờ Việt Nam (UTC+7), gồm cả hai đầu.
/// Các điều kiện được ghép bằng AND.
/// </summary>
public sealed record AuditQuery(
    string? Search = null,
    string? Actor = null,
    string? Action = null,
    string? Target = null,
    DateOnly? From = null,
    DateOnly? To = null)
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public DateTimeOffset? FromUtc => From is { } day
        ? new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), VietnamOffset).ToUniversalTime()
        : null;

    /// <summary>Cận trên không gồm: đầu ngày kế tiếp theo giờ Việt Nam.</summary>
    public DateTimeOffset? ToUtcExclusive => To is { } day
        ? new DateTimeOffset(day.AddDays(1).ToDateTime(TimeOnly.MinValue), VietnamOffset).ToUniversalTime()
        : null;

    public bool HasFilter => !string.IsNullOrWhiteSpace(Search) || !string.IsNullOrWhiteSpace(Actor) || !string.IsNullOrWhiteSpace(Action)
        || !string.IsNullOrWhiteSpace(Target) || From is not null || To is not null;
}
