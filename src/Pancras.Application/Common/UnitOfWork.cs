namespace Pancras.Application.Common;

/// <summary>
/// Ranh giới giao dịch của một thao tác ghi dữ liệu (docs/02-coding-rules/use-cases-and-results.md mục "Giao dịch").
/// Handler mở một scope, đọc/khóa/đổi dữ liệu, thêm nhật ký bằng <c>audit.Stage</c>, rồi <see cref="IUnitOfWorkScope.CommitAsync"/>.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Mở giao dịch. Không lồng nhau: mở khi đã có scope đang mở là lỗi lập trình.</summary>
    Task<IUnitOfWorkScope> BeginAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Một giao dịch đang mở. Thoát (dispose) mà chưa <see cref="CommitAsync"/> thì hủy giao dịch và bỏ mọi thay đổi chưa lưu.
/// </summary>
public interface IUnitOfWorkScope : IAsyncDisposable
{
    /// <summary>
    /// Lưu mọi thay đổi (dữ liệu và nhật ký) rồi commit. Thất bại thì giao dịch bị hủy, thay đổi chưa lưu bị bỏ và
    /// kết quả cho biết lý do; scope không dùng lại được sau lần gọi này.
    /// </summary>
    Task<SaveOutcome> CommitAsync(CancellationToken cancellationToken);
}
