using Pancras.Application.Auditing;
using Pancras.Application.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Inventory;

/// <summary>Ai nhận thông báo khi phiếu chuyển có bước mới.</summary>
[Flags]
internal enum TransferAudience
{
    None = 0,
    Creator = 1,
    Finance = 2,
    SourceLead = 4,
    SourceStaff = 8,
    DestinationStaff = 16
}

/// <summary>
/// Lịch sử và thông báo của phiếu chuyển. Mỗi bước ghi một dòng lịch sử (ai, lúc nào, lý do nếu có)
/// và gửi thông báo cho những người cần biết, trừ chính người vừa thao tác.
/// Cả hai chỉ được thêm vào ngữ cảnh; handler gọi CommitAsync một lần để lưu cùng thay đổi của phiếu.
/// </summary>
internal static class TransferTrail
{
    public static Task RecordAsync(
        IInventoryRepository repository,
        TransferSlip slip,
        TransferEventKind kind,
        IRequestInfo? request,
        TimeProvider? clock,
        string? note,
        CancellationToken cancellationToken) =>
        repository.AddTransferEventAsync(
            TransferEvent.Create(slip.Id, kind, request?.ActorCode, (clock ?? TimeProvider.System).GetUtcNow(), note),
            cancellationToken);

    public static async Task NotifyAsync(
        IInventoryRepository repository,
        IEmployeeRepository? employees,
        TransferSlip slip,
        Guid actorId,
        string step,
        TransferAudience audience,
        TimeProvider? clock,
        CancellationToken cancellationToken)
    {
        if (employees is null || audience == TransferAudience.None)
        {
            return;
        }

        var people = (await employees.ListReadOnlyAsync(cancellationToken)).Where(person => person.IsActive).ToArray();
        var recipients = new HashSet<Guid>();
        foreach (var person in people)
        {
            var staffAt = (Guid? location) => location is Guid place
                && person.StockLocationId == place
                && (InventoryRoles.IsShowroomLead(person.Role) || InventoryRoles.IsWarehouseKeeper(person.Role));
            var matches =
                (audience.HasFlag(TransferAudience.Creator) && slip.RequestedById != Guid.Empty && person.Id == slip.RequestedById)
                || (audience.HasFlag(TransferAudience.Finance) && InventoryRoles.IsFinance(person.Role))
                || (audience.HasFlag(TransferAudience.SourceLead) && InventoryRoles.IsShowroomLead(person.Role) && person.StockLocationId == slip.SourceLocationId)
                || (audience.HasFlag(TransferAudience.SourceStaff) && staffAt(slip.SourceLocationId))
                || (audience.HasFlag(TransferAudience.DestinationStaff) && staffAt(slip.DestinationLocationId));
            if (matches && person.Id != actorId)
            {
                recipients.Add(person.Id);
            }
        }

        // Thông báo cũ của phiếu này đã lỗi thời (ví dụ "chờ duyệt" sau khi đã duyệt) nên thay bằng thông báo mới.
        await repository.RemoveNoticesAsync(slip.Id, cancellationToken);
        var occurredAt = (clock ?? TimeProvider.System).GetUtcNow();
        foreach (var recipient in recipients)
        {
            await repository.AddNoticeAsync(
                StockNotice.Create(recipient, slip.SourceLocationId, slip.Id, StockNoticeKind.Transfer, step, occurredAt),
                cancellationToken);
        }
    }

    /// <summary>Mô tả mặt hàng thiếu tồn để trang ghép vào thông báo lỗi: "SKU (còn X, cần Y)".</summary>
    public static async Task<string> ShortageAsync(
        IInventoryRepository repository,
        TransferSlip slip,
        IReadOnlyDictionary<Guid, StockBalance> sources,
        bool alreadyReserved,
        CancellationToken cancellationToken)
    {
        var items = (await repository.ListItemChoicesAsync(cancellationToken)).ToDictionary(item => item.Id);
        var parts = new List<string>();
        foreach (var group in slip.Lines.GroupBy(line => line.CatalogItemId))
        {
            var needed = group.Sum(line => line.Quantity);
            var available = sources.TryGetValue(group.Key, out var balance) ? (alreadyReserved ? Math.Min(balance.Reserved, balance.OnHand) : balance.Available) : 0;
            if (available < needed)
            {
                var code = items.TryGetValue(group.Key, out var item) ? item.Code : group.Key.ToString("N")[..8];
                parts.Add($"{code} (còn {available}, cần {needed})");
            }
        }

        return string.Join(", ", parts);
    }
}
