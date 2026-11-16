using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

public sealed class ListStockNoticesHandler(IInventoryRepository repository)
{
    public Task<IReadOnlyList<StockNotice>> HandleAsync(Guid employeeId, CancellationToken cancellationToken) =>
        repository.ListNoticesAsync(employeeId, cancellationToken);

    public async Task<int> CountUnseenAsync(Guid employeeId, CancellationToken cancellationToken) =>
        (await repository.ListNoticesAsync(employeeId, cancellationToken)).Count(notice => !notice.Seen);

    public async Task<StockNotice?> OpenAsync(Guid noticeId, Guid employeeId, CancellationToken cancellationToken)
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
