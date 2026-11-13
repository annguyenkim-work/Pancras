using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Catalog;
using Pancras.Application.Purchasing;
using Pancras.Domain.Purchasing;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Infrastructure.Tests;

/// <summary>
/// Ghi và đọc đơn mua qua EF thật (nhà cung cấp lưu trữ InMemory). Các test ở tầng Application dùng bộ giả nên không bắt được
/// lỗi ánh xạ, ví dụ khoản chi phí mới mang Guid sinh sẵn bị EF gửi UPDATE thay vì INSERT.
/// Mỗi bước dùng một DbContext mới, giống mỗi request web.
/// </summary>
public class PurchasePersistenceTests
{
    private readonly DbContextOptions<PancrasDbContext> _options = new DbContextOptionsBuilder<PancrasDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options;

    private PancrasDbContext NewContext() => new(_options);

    private static PurchaseOrderRepository Orders(PancrasDbContext context) =>
        new(context, NullLogger<PurchaseOrderRepository>.Instance);

    private static PurchaseOrder NewOrder(params (Guid Item, int Quantity, decimal Fob)[] lines) =>
        PurchaseOrder.TryCreate(
            "PO-INT-" + Guid.NewGuid().ToString("N")[..6],
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "CONT",
            null,
            null,
            null,
            25000m,
            0.10m,
            lines.Select(line => (line.Item, line.Quantity, line.Fob, 0.10m, 0m, 0m, 0m)).ToArray())!;

    private async Task<Guid> SaveNewOrderAsync(PurchaseOrder order)
    {
        await using var context = NewContext();
        Assert.True(await Orders(context).TryAddAsync(order, CancellationToken.None));
        return order.Id;
    }

    private async Task<PurchaseOrder> LoadAsync(Guid id)
    {
        await using var context = NewContext();
        return (await Orders(context).FindReadOnlyAsync(id, CancellationToken.None))!;
    }

    [Fact]
    public async Task AChargeAddedToAnAlreadySavedOrder_IsInserted_NotUpdated()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var id = await SaveNewOrderAsync(NewOrder((first, 2, 10m), (second, 3, 30m)));

        await using (var context = NewContext())
        {
            var handler = new AllocateChargeCommandHandler(
                Orders(context),
                new GetCatalogMeasuresHandler(new CatalogItemRepository(context), NullLogger<GetCatalogMeasuresHandler>.Instance),
                NullLogger<AllocateChargeCommandHandler>.Instance);

            var result = await handler.HandleAsync(
                new AllocateChargeCommand { PurchaseOrderId = id, Amount = 1000m, Method = ChargeSplitMethod.FobValue, Label = "THC", Group = ChargeGroup.Logistics },
                CancellationToken.None);

            Assert.True(result.IsSuccess, "Thêm khoản chi phí vào đơn đã lưu phải thành công (lỗi cũ: EF gửi UPDATE cho dòng chưa có).");
        }

        var saved = await LoadAsync(id);
        var charge = Assert.Single(saved.Charges);
        Assert.Equal("THC", charge.Label);
        Assert.Equal(ChargeGroup.Logistics, charge.Group);
        Assert.Equal(1000m, charge.Shares.Sum(share => share.Amount));
        Assert.Equal(2, charge.Shares.Count);
    }

    [Fact]
    public async Task EditingLinesOfASavedOrder_ReSplitsTheChargesAndPersistsNewLines()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var order = NewOrder((first, 2, 10m), (second, 3, 30m));
        Assert.True(order.TryAddCharge(40m, ChargeSplitMethod.FobValue, out var chargeId, "Cước"));
        Assert.True(order.TryAllocate(chargeId, order.Lines.ToDictionary(line => line.Id, line => line.FobUsd), manual: false));
        Assert.True(order.TryAssignGroup(chargeId, ChargeGroup.Logistics, false));
        var id = await SaveNewOrderAsync(order);

        await using (var context = NewContext())
        {
            var repository = Orders(context);
            var loaded = (await repository.FindForUpdateAsync(id, CancellationToken.None))!;
            var keep = loaded.Lines[0];
            var third = Guid.CreateVersion7();
            Assert.True(loaded.TryUpdateLines(
                [
                    (keep.Id, keep.CatalogItemId, 2, 30m, 0.10m, 0m, 0m, 0m),
                    (loaded.Lines[1].Id, loaded.Lines[1].CatalogItemId, 3, 30m, 0.10m, 0m, 0m, 0m),
                    ((Guid?)null, third, 1, 20m, 0.10m, 0m, 0m, 0m)
                ],
                out var changed));
            Assert.True(changed);
            Assert.True(loaded.TryAllocate(loaded.Charges[0].Id, loaded.Lines.ToDictionary(line => line.Id, line => line.FobUsd), manual: false));

            Assert.True(await repository.CommitAsync(CancellationToken.None), "Lưu đơn đã sửa (thêm dòng mới, chia lại chi phí) phải thành công.");
        }

        var saved = await LoadAsync(id);
        Assert.Equal(3, saved.Lines.Count);
        var shares = saved.Charges.Single().Shares;
        Assert.Equal(3, shares.Count);
        Assert.Equal(40m, shares.Sum(share => share.Amount));
    }

    [Fact]
    public async Task RemovingALine_AndDeletingACharge_RemoveTheirRowsFromTheStore()
    {
        var order = NewOrder((Guid.CreateVersion7(), 2, 10m), (Guid.CreateVersion7(), 3, 30m));
        Assert.True(order.TryAddCharge(40m, ChargeSplitMethod.FobValue, out var chargeId, "Cước"));
        Assert.True(order.TryAllocate(chargeId, order.Lines.ToDictionary(line => line.Id, line => line.FobUsd), manual: false));
        Assert.True(order.TryAddCharge(10m, ChargeSplitMethod.FobValue, out var other, "Phụ"));
        Assert.True(order.TryAllocate(other, order.Lines.ToDictionary(line => line.Id, line => line.FobUsd), manual: false));
        var id = await SaveNewOrderAsync(order);

        await using (var context = NewContext())
        {
            var repository = Orders(context);
            var loaded = (await repository.FindForUpdateAsync(id, CancellationToken.None))!;
            var kept = loaded.Lines[0];
            Assert.True(loaded.TryUpdateLines([(kept.Id, kept.CatalogItemId, 2, 10m, 0.10m, 0m, 0m, 0m)], out _));
            loaded.DropCharge(other);
            Assert.True(await repository.CommitAsync(CancellationToken.None));
        }

        var saved = await LoadAsync(id);
        Assert.Single(saved.Lines);
        var charge = Assert.Single(saved.Charges);
        Assert.Equal(chargeId, charge.Id);
        Assert.Single(charge.Shares);
        await using var check = NewContext();
        Assert.Equal(1, await check.PurchaseCharges.CountAsync());
        Assert.Equal(1, await check.PurchaseChargeShares.CountAsync());
    }

    [Fact]
    public async Task ANewOrderSavedTogetherWithItsCharges_PersistsTheWholeGraph()
    {
        var order = NewOrder((Guid.CreateVersion7(), 2, 10m), (Guid.CreateVersion7(), 3, 30m));
        Assert.True(order.TryAddCharge(900m, ChargeSplitMethod.Quantity, out var chargeId, "THC"));
        Assert.True(order.TryAllocate(chargeId, order.Lines.ToDictionary(line => line.Id, line => (decimal)line.Quantity), manual: false));
        Assert.True(order.TryAssignGroup(chargeId, ChargeGroup.Logistics, false));

        var saved = await LoadAsync(await SaveNewOrderAsync(order));

        var charge = Assert.Single(saved.Charges);
        Assert.Equal(ChargeGroup.Logistics, charge.Group);
        Assert.Equal(900m, charge.Shares.Sum(share => share.Amount));
    }

    [Fact]
    public async Task Attachments_CanBeAddedListedAndFound()
    {
        var orderId = await SaveNewOrderAsync(NewOrder((Guid.CreateVersion7(), 1, 10m)));
        var attachment = PurchaseAttachment.TryCreate(orderId, AttachmentKind.CustomsDeclaration, "to-khai.pdf", "0123456789abcdef0123456789abcdef.pdf", 12, DateTimeOffset.UtcNow, "an")!;

        await using (var context = NewContext())
        {
            await new PurchaseAttachmentRepository(context).AddAsync(attachment, CancellationToken.None);
        }

        await using (var context = NewContext())
        {
            var repository = new PurchaseAttachmentRepository(context);
            var listed = await repository.ListAsync(orderId, CancellationToken.None);
            var found = await repository.FindAsync(attachment.Id, CancellationToken.None);
            Assert.Equal("to-khai.pdf", Assert.Single(listed).FileName);
            Assert.Equal(AttachmentKind.CustomsDeclaration, found!.Kind);
        }
    }
}
