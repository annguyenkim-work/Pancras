using Pancras.Application.Inventory;
using Pancras.Domain.Notifications;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Notifications;

public sealed class ListNotificationsHandler(IInventoryRepository repository)
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

    public Task<int> CountUnseenAsync(Guid employeeId, CancellationToken cancellationToken) =>
        repository.CountUnseenNoticesAsync(employeeId, cancellationToken);

    public async Task<Notification?> OpenAsync(Guid noticeId, Guid employeeId, CancellationToken cancellationToken)
    {
        var notice = await repository.FindNoticeForUpdateAsync(noticeId, cancellationToken);
        if (notice is null || notice.RecipientEmployeeId != employeeId)
        {
            return null;
        }

        notice.MarkSeen();
        await repository.CommitAsync(cancellationToken);
        return notice;
    }
}
