using Pancras.Application.Inventory;
using Pancras.Domain.Notifications;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Notifications;

public sealed class ListNotificationsHandler(IInventoryRepository repository, Pancras.Application.Common.IUnitOfWork unitOfWork)
{
    /// <summary>Một trang thông báo của người nhận (số trang vượt cuối thì lấy trang cuối).</summary>
    public async Task<(IReadOnlyList<Notification> Items, int Total, int PageNumber)> HandleAsync(
        Guid employeeId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var size = pageSize < 1 ? Pancras.Application.Common.PageSizes.Default : pageSize;
        var page = await repository.ListNoticesPageAsync(employeeId, Math.Max(1, pageNumber), size, cancellationToken);
        var lastPage = Math.Max(1, (page.Total + size - 1) / size);
        if (pageNumber > lastPage)
        {
            page = await repository.ListNoticesPageAsync(employeeId, lastPage, size, cancellationToken);
            return (page.Items, page.Total, lastPage);
        }

        return (page.Items, page.Total, Math.Max(1, pageNumber));
    }

    /// <summary>Số thứ tự của các phiếu chuyển mà thông báo nhắc tới, để hiện mã CK-000123 thay cho mã cũ.</summary>
    public async Task<IReadOnlyDictionary<Guid, int>> TransferNumbersAsync(IEnumerable<Notification> notices, CancellationToken cancellationToken) =>
        await repository.ListTransferNumbersAsync(
            notices.Where(notice => notice.Kind == NotificationKind.Transfer).Select(notice => notice.DocumentId).Distinct().ToArray(),
            cancellationToken);

    public Task<int> CountUnseenAsync(Guid employeeId, CancellationToken cancellationToken) =>
        repository.CountUnseenNoticesAsync(employeeId, cancellationToken);

    public async Task<Notification?> OpenAsync(Guid noticeId, Guid employeeId, CancellationToken cancellationToken)
    {
        await using var scope = await unitOfWork.BeginAsync(cancellationToken);
        var notice = await repository.FindNoticeForUpdateAsync(noticeId, cancellationToken);
        if (notice is null || notice.RecipientEmployeeId != employeeId)
        {
            return null;
        }

        notice.MarkSeen();
        await scope.CommitAsync(cancellationToken);
        return notice;
    }
}
