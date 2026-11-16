using Pancras.Domain.Notifications;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Notifications;

/// <summary>
/// Ghi thông báo từ module không phải kho (ví dụ mua hàng). Thông báo được thêm vào cùng đơn vị lưu của chứng từ,
/// nên được lưu trong lần commit của chính handler gọi.
/// </summary>
public interface INoticeStore
{
    Task AddAsync(Notification notice, CancellationToken cancellationToken);

    /// <summary>Xóa thông báo cũ của một chứng từ (bước đã qua nên thông báo lỗi thời).</summary>
    Task RemoveForDocumentAsync(Guid documentId, CancellationToken cancellationToken);
}
