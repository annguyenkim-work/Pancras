using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

internal sealed class FakeTransferAttachments : ITransferAttachmentRepository
{
    public List<TransferAttachment> Items { get; } = [];

    public Task<IReadOnlyList<TransferAttachment>> ListAsync(Guid transferSlipId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TransferAttachment>>(Items.Where(item => item.TransferSlipId == transferSlipId).ToArray());

    public Task<TransferAttachment?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<TransferAttachment>>> ListForSlipsAsync(IReadOnlyCollection<Guid> transferSlipIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<TransferAttachment>>>(
            Items.Where(item => transferSlipIds.Contains(item.TransferSlipId)).GroupBy(item => item.TransferSlipId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<TransferAttachment>)group.ToList()));

    public Task AddAsync(TransferAttachment attachment, CancellationToken cancellationToken)
    {
        Items.Add(attachment);
        return Task.CompletedTask;
    }

    public void Attach(Guid slipId) =>
        Items.Add(TransferAttachment.TryCreate(slipId, "bien-ban.pdf", Guid.CreateVersion7().ToString("N") + ".pdf", 100, DateTimeOffset.UtcNow, "AnNK")!);
}

/// <summary>Đi hết luồng đề nghị, duyệt, xuất kho để các test khác có sẵn phiếu đang chuyển.</summary>
internal static class TransferFlow
{
    public static async Task<Result<Guid>> RequestAsync(FakeInventoryStore store, CreateTransferSlipCommand command) =>
        await new CreateTransferRequestCommandHandler(store, NullLogger<CreateTransferRequestCommandHandler>.Instance)
            .HandleAsync(command, CancellationToken.None);

    public static async Task<Result<Guid>> ShipAsync(FakeInventoryStore store, CreateTransferSlipCommand command)
    {
        var requested = await RequestAsync(store, command);
        if (requested.IsFailure)
        {
            return requested;
        }

        var approved = await new DecideTransferCommandHandler(store, NullLogger<DecideTransferCommandHandler>.Instance)
            .HandleAsync(new DecideTransferCommand(requested.Value, TestActors.Admin, true), CancellationToken.None);
        if (approved.IsFailure)
        {
            return approved;
        }

        return await new ShipTransferCommandHandler(store, NullLogger<ShipTransferCommandHandler>.Instance)
            .HandleAsync(new ShipTransferCommand(requested.Value, command.Actor, "SEAL-1", "Anh Bình"), CancellationToken.None);
    }
}

public class TransferSlipHandlerTests
{
    [Fact]
    public async Task Request_DoesNotChangeStock_AndWaitsForApproval()
    {
        var store = Store();

        var result = await TransferFlow.RequestAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 2));

        Assert.True(result.IsSuccess);
        Assert.Equal(TransferStatus.Requested, store.Store.Transfers[0].Status);
        Assert.Equal(115, store.SourceBalance.OnHand);
        Assert.Equal(0, store.DestinationBalance.InTransit);
    }

    [Fact]
    public async Task Request_RejectsTheSameLocation_AndUnknownLocations()
    {
        var store = Store();
        var same = await TransferFlow.RequestAsync(store.Store, Command(store.Source.Id, store.Source.Id, store.ItemId, 2));
        store.Store.Locations.Remove(store.Destination);
        var deleted = await TransferFlow.RequestAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 2));

        Assert.Equal(StockError.InvalidTransfer, same.StockError);
        Assert.Equal(StockError.InvalidTransfer, deleted.StockError);
        Assert.Empty(store.Store.Transfers);
    }

    [Fact]
    public async Task Approve_ChecksAvailableStock_AndRecordsTheApprover()
    {
        var store = Store(onHand: 10, reserved: 8);
        var id = (await TransferFlow.RequestAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 3))).Value;
        var handler = new DecideTransferCommandHandler(store.Store, NullLogger<DecideTransferCommandHandler>.Instance);

        var shortage = await handler.HandleAsync(new DecideTransferCommand(id, TestActors.Admin, true), CancellationToken.None);
        store.SourceBalance.TryAdjust(5);
        var ok = await handler.HandleAsync(new DecideTransferCommand(id, TestActors.Admin, true), CancellationToken.None);

        Assert.Equal(StockError.ShipmentRejected, shortage.StockError);
        Assert.True(ok.IsSuccess);
        Assert.Equal(TransferStatus.Approved, store.Store.Transfers[0].Status);
    }

    [Fact]
    public async Task Approve_IsLimitedToFinanceOrTheLeadOfTheSourceLocation()
    {
        var store = Store();
        var id = (await TransferFlow.RequestAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 1))).Value;
        var handler = new DecideTransferCommandHandler(store.Store, NullLogger<DecideTransferCommandHandler>.Instance);

        var keeper = await handler.HandleAsync(new DecideTransferCommand(id, TestActors.Keeper(store.Source.Id), true), CancellationToken.None);
        var otherLead = await handler.HandleAsync(new DecideTransferCommand(id, TestActors.Lead(store.Destination.Id), true), CancellationToken.None);
        var lead = await handler.HandleAsync(new DecideTransferCommand(id, TestActors.Lead(store.Source.Id), true), CancellationToken.None);

        Assert.Equal(StockError.ApprovalForbidden, keeper.StockError);
        Assert.Equal(StockError.ApprovalForbidden, otherLead.StockError);
        Assert.True(lead.IsSuccess);
    }

    [Fact]
    public async Task Reject_ClosesTheRequest_WithoutTouchingStock()
    {
        var store = Store();
        var id = (await TransferFlow.RequestAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 1))).Value;

        var result = await new DecideTransferCommandHandler(store.Store, NullLogger<DecideTransferCommandHandler>.Instance)
            .HandleAsync(new DecideTransferCommand(id, TestActors.Admin, false, "Chưa cần"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TransferStatus.Rejected, store.Store.Transfers[0].Status);
        Assert.Equal("Chưa cần", store.Store.Transfers[0].ResolutionNote);
        Assert.Equal(115, store.SourceBalance.OnHand);
    }

    [Fact]
    public async Task Ship_RequiresApproval_AndMovesStockOnce()
    {
        var store = Store(onHand: 115);
        var id = (await TransferFlow.RequestAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 2))).Value;
        var ship = new ShipTransferCommandHandler(store.Store, NullLogger<ShipTransferCommandHandler>.Instance);

        var early = await ship.HandleAsync(new ShipTransferCommand(id, TestActors.Admin), CancellationToken.None);
        await new DecideTransferCommandHandler(store.Store, NullLogger<DecideTransferCommandHandler>.Instance)
            .HandleAsync(new DecideTransferCommand(id, TestActors.Admin, true), CancellationToken.None);
        var first = await ship.HandleAsync(new ShipTransferCommand(id, TestActors.Admin, "SEAL-9", "Chú Ba"), CancellationToken.None);
        var second = await ship.HandleAsync(new ShipTransferCommand(id, TestActors.Admin), CancellationToken.None);

        Assert.Equal(StockError.ShipmentRejected, early.StockError);
        Assert.True(first.IsSuccess);
        Assert.Equal(StockError.ShipmentRejected, second.StockError);
        Assert.Equal(113, store.SourceBalance.OnHand);
        Assert.Equal(2, store.DestinationBalance.InTransit);
        Assert.Equal("SEAL-9", store.Store.Transfers[0].SealNumber);
        Assert.Equal("Chú Ba", store.Store.Transfers[0].Carrier);
    }

    [Fact]
    public async Task Ship_IsLimitedToTheSourceLocation()
    {
        var store = Store();
        var id = (await TransferFlow.RequestAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 1))).Value;
        await new DecideTransferCommandHandler(store.Store, NullLogger<DecideTransferCommandHandler>.Instance)
            .HandleAsync(new DecideTransferCommand(id, TestActors.Admin, true), CancellationToken.None);

        var result = await new ShipTransferCommandHandler(store.Store, NullLogger<ShipTransferCommandHandler>.Instance)
            .HandleAsync(new ShipTransferCommand(id, TestActors.Keeper(store.Destination.Id)), CancellationToken.None);

        Assert.Equal(StockError.Forbidden, result.StockError);
        Assert.Equal(115, store.SourceBalance.OnHand);
    }

    [Fact]
    public async Task Receive_FullQuantity_MovesInTransitIntoOnHand_AndCloses()
    {
        var store = Store(onHand: 10, reserved: 3, destinationOnHand: 4, destinationReserved: 2);
        var id = (await TransferFlow.ShipAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 3))).Value;

        var result = await Receipt(store.Store).HandleAsync(new ConfirmTransferCommand(id, TestActors.Admin), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, store.DestinationBalance.InTransit);
        Assert.Equal(7, store.DestinationBalance.OnHand);
        Assert.Equal(2, store.DestinationBalance.Reserved);
        Assert.Equal(TransferStatus.Received, store.Store.Transfers[0].Status);
        Assert.Equal(3, store.Store.Transfers[0].Lines[0].ReceivedQuantity);
    }

    [Theory]
    [InlineData(3, 5)]
    [InlineData(5, 4)]
    public async Task Receive_AnyDifference_NeedsReasonAndAttachment_ThenWaitsForResolution(int shipped, int actual)
    {
        var store = Store(onHand: 20);
        var attachments = new FakeTransferAttachments();
        var id = (await TransferFlow.ShipAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, shipped))).Value;
        var slip = store.Store.Transfers[0];
        var actuals = new[] { new ReceivedLineInput(slip.Lines[0].Id, actual) };
        var handler = Receipt(store.Store, attachments);

        var noReason = await handler.HandleAsync(new ConfirmTransferCommand(id, TestActors.Admin, actuals), CancellationToken.None);
        var noFile = await handler.HandleAsync(new ConfirmTransferCommand(id, TestActors.Admin, actuals, "Đếm lại"), CancellationToken.None);
        attachments.Attach(id);
        var ok = await handler.HandleAsync(new ConfirmTransferCommand(id, TestActors.Admin, actuals, "Đếm lại"), CancellationToken.None);

        Assert.Equal(StockError.ReasonRequired, noReason.StockError);
        Assert.Equal(StockError.AttachmentRequired, noFile.StockError);
        Assert.True(ok.IsSuccess);
        Assert.Equal(TransferStatus.ReceivedWithDiscrepancy, slip.Status);
        Assert.Equal(actual, store.DestinationBalance.OnHand);
        Assert.Equal(0, store.DestinationBalance.InTransit);
        Assert.Equal("Đếm lại", slip.ReceiveNote);
    }

    [Fact]
    public async Task Receive_ASecondTime_IsRejected()
    {
        var store = Store();
        var id = (await TransferFlow.ShipAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 2))).Value;
        await Receipt(store.Store).HandleAsync(new ConfirmTransferCommand(id, TestActors.Admin), CancellationToken.None);

        var again = await Receipt(store.Store).HandleAsync(new ConfirmTransferCommand(id, TestActors.Admin), CancellationToken.None);

        Assert.Equal(StockError.ReceiptRejected, again.StockError);
        Assert.Equal(2, store.DestinationBalance.OnHand);
    }

    [Fact]
    public async Task Resolve_IsFinanceOnly_NeedsNote_AndClosesTheSlip()
    {
        var store = Store(onHand: 20);
        var attachments = new FakeTransferAttachments();
        var id = (await TransferFlow.ShipAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 4))).Value;
        attachments.Attach(id);
        await Receipt(store.Store, attachments).HandleAsync(
            new ConfirmTransferCommand(id, TestActors.Admin, [new ReceivedLineInput(store.Store.Transfers[0].Lines[0].Id, 3)], "Thiếu 1"),
            CancellationToken.None);
        var resolve = new ResolveTransferDiscrepancyCommandHandler(store.Store, NullLogger<ResolveTransferDiscrepancyCommandHandler>.Instance);

        var keeper = await resolve.HandleAsync(new ResolveTransferCommand(id, TestActors.Keeper(store.Destination.Id), "OK"), CancellationToken.None);
        var empty = await resolve.HandleAsync(new ResolveTransferCommand(id, TestActors.Accountant, " "), CancellationToken.None);
        var ok = await resolve.HandleAsync(new ResolveTransferCommand(id, TestActors.Accountant, "Ghi nhận hao hụt"), CancellationToken.None);

        Assert.Equal(StockError.ApprovalForbidden, keeper.StockError);
        Assert.Equal(StockError.ReasonRequired, empty.StockError);
        Assert.True(ok.IsSuccess);
        Assert.Equal(TransferStatus.Received, store.Store.Transfers[0].Status);
        Assert.Equal("Ghi nhận hao hụt", store.Store.Transfers[0].ResolutionNote);
    }

    [Fact]
    public async Task Cancel_BeforeShipment_LeavesStockAlone()
    {
        var store = Store();
        var id = (await TransferFlow.RequestAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 2))).Value;

        var result = await Cancel(store.Store).HandleAsync(new ConfirmTransferCommand(id, TestActors.Admin), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TransferStatus.Cancelled, store.Store.Transfers[0].Status);
        Assert.Equal(115, store.SourceBalance.OnHand);
    }

    [Fact]
    public async Task Cancel_IsRejectedOnceShipped_ReceivedOrCancelledTwice()
    {
        var store = Store(onHand: 10);
        var shipped = (await TransferFlow.ShipAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 4))).Value;
        var received = (await TransferFlow.ShipAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 2))).Value;
        await Receipt(store.Store).HandleAsync(new ConfirmTransferCommand(received, TestActors.Admin), CancellationToken.None);
        var requested = (await TransferFlow.RequestAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 1))).Value;
        await Cancel(store.Store).HandleAsync(new ConfirmTransferCommand(requested, TestActors.Admin), CancellationToken.None);

        var whileInTransit = await Cancel(store.Store).HandleAsync(new ConfirmTransferCommand(shipped, TestActors.Admin), CancellationToken.None);
        var afterReceipt = await Cancel(store.Store).HandleAsync(new ConfirmTransferCommand(received, TestActors.Admin), CancellationToken.None);
        var twice = await Cancel(store.Store).HandleAsync(new ConfirmTransferCommand(requested, TestActors.Admin), CancellationToken.None);

        Assert.Equal(StockError.CancelRejected, whileInTransit.StockError);
        Assert.Equal(StockError.CancelRejected, afterReceipt.StockError);
        Assert.Equal(StockError.CancelRejected, twice.StockError);
        Assert.Equal(TransferStatus.InTransit, store.Store.Transfers.Single(slip => slip.Id == shipped).Status);
        Assert.Equal(4, store.DestinationBalance.InTransit);
        Assert.Equal(2, store.DestinationBalance.OnHand);
        Assert.Equal(4, store.SourceBalance.OnHand);
    }

    [Fact]
    public async Task Receive_ZeroActual_IsAccepted_AsADiscrepancy_WithoutAMovement()
    {
        var store = Store(onHand: 20);
        var attachments = new FakeTransferAttachments();
        var id = (await TransferFlow.ShipAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 3))).Value;
        var slip = store.Store.Transfers[0];
        attachments.Attach(id);

        var result = await Receipt(store.Store, attachments).HandleAsync(
            new ConfirmTransferCommand(id, TestActors.Admin, [new ReceivedLineInput(slip.Lines[0].Id, 0)], "Mất hàng"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TransferStatus.ReceivedWithDiscrepancy, slip.Status);
        Assert.Equal(0, store.DestinationBalance.OnHand);
        Assert.Equal(0, store.DestinationBalance.InTransit);
    }

    [Fact]
    public async Task Check_Receipt_WritesNothing_AndCountsNewFilesTowardsTheEvidenceRule()
    {
        var store = Store(onHand: 20);
        var id = (await TransferFlow.ShipAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 3))).Value;
        var slip = store.Store.Transfers[0];
        var handler = Receipt(store.Store, new FakeTransferAttachments());
        var command = new ConfirmTransferCommand(id, TestActors.Admin, [new ReceivedLineInput(slip.Lines[0].Id, 2)], "Vỡ 1");

        var noReason = await handler.CheckAsync(command with { Note = null }, 1, CancellationToken.None);
        var noFile = await handler.CheckAsync(command, 0, CancellationToken.None);
        var ok = await handler.CheckAsync(command, 1, CancellationToken.None);

        Assert.Equal(StockError.ReasonRequired, noReason.StockError);
        Assert.Equal(StockError.AttachmentRequired, noFile.StockError);
        Assert.True(ok.IsSuccess);
        Assert.Equal(TransferStatus.InTransit, slip.Status);
        Assert.Equal(0, store.DestinationBalance.OnHand);
        Assert.Equal(3, store.DestinationBalance.InTransit);
    }

    [Fact]
    public async Task CheckFiles_RejectsTheWholeBatch_WhenOneFileIsTooLargeOrInvalid()
    {
        var store = Store(onHand: 20);
        var id = (await TransferFlow.ShipAsync(store.Store, Command(store.Source.Id, store.Destination.Id, store.ItemId, 3))).Value;
        var handler = new UploadTransferAttachmentCommandHandler(
            store.Store, new FakeTransferAttachments(), null!, NullLogger<UploadTransferAttachmentCommandHandler>.Instance);
        byte[] pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34];
        var good = new PendingTransferFile("a.pdf", 100, pdf);

        var ok = await handler.CheckAsync(id, [good], TestActors.Admin, CancellationToken.None);
        var tooBig = await handler.CheckAsync(id, [good, new PendingTransferFile("b.pdf", 11L * 1024 * 1024, pdf)], TestActors.Admin, CancellationToken.None);
        var badType = await handler.CheckAsync(id, [good, new PendingTransferFile("c.exe", 100, pdf)], TestActors.Admin, CancellationToken.None);
        var fakePdf = await handler.CheckAsync(id, [good, new PendingTransferFile("d.pdf", 100, [1, 2, 3, 4])], TestActors.Admin, CancellationToken.None);

        Assert.True(ok.IsSuccess);
        Assert.Equal(StockError.AttachmentTooLarge, tooBig.StockError);
        Assert.Equal(StockError.AttachmentInvalid, badType.StockError);
        Assert.Equal(StockError.AttachmentInvalid, fakePdf.StockError);
    }

    private static ConfirmTransferReceiptCommandHandler Receipt(FakeInventoryStore store, FakeTransferAttachments? attachments = null) =>
        new(store, NullLogger<ConfirmTransferReceiptCommandHandler>.Instance, attachments: attachments);

    private static CancelTransferCommandHandler Cancel(FakeInventoryStore store) =>
        new(store, NullLogger<CancelTransferCommandHandler>.Instance);

    private static CreateTransferSlipCommand Command(Guid sourceId, Guid destinationId, Guid itemId, int quantity) =>
        new()
        {
            SourceLocationId = sourceId,
            DestinationLocationId = destinationId,
            Note = "Xe sáng",
            Actor = TestActors.Admin,
            Lines = [new TransferLineInput(itemId, quantity)]
        };

    private static TransferFixture Store(
        int onHand = 115,
        int reserved = 0,
        int destinationOnHand = 0,
        int destinationReserved = 0)
    {
        var store = new FakeInventoryStore();
        var source = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var destination = StockLocation.Create("Showroom B", StockLocationKind.Showroom);
        var itemId = Guid.CreateVersion7();
        store.Locations.Add(source);
        store.Locations.Add(destination);
        var sourceBalance = StockBalance.Create(itemId, source.Id, onHand, reserved, 0);
        var destinationBalance = StockBalance.Create(itemId, destination.Id, destinationOnHand, destinationReserved, 0);
        store.Balances.Add(sourceBalance);
        store.Balances.Add(destinationBalance);
        return new TransferFixture(store, source, destination, itemId, sourceBalance, destinationBalance);
    }

    private sealed record TransferFixture(
        FakeInventoryStore Store,
        StockLocation Source,
        StockLocation Destination,
        Guid ItemId,
        StockBalance SourceBalance,
        StockBalance DestinationBalance);
}

public class TransferRequestUpdateTests
{
    private static (FakeInventoryStore Store, StockLocation A, StockLocation B, Guid Item, Guid Id) Setup()
    {
        var store = new FakeInventoryStore();
        var a = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        var b = StockLocation.Create("Kho B", StockLocationKind.Warehouse);
        var item = Guid.CreateVersion7();
        store.Locations.Add(a);
        store.Locations.Add(b);
        store.Balances.Add(StockBalance.Create(item, a.Id, 50, 0, 0));
        var request = new CreateTransferSlipCommand
        {
            SourceLocationId = a.Id,
            DestinationLocationId = b.Id,
            Actor = TestActors.Keeper(b.Id),
            Note = "cũ",
            Lines = [new TransferLineInput(item, 2)]
        };
        var id = TransferFlow.RequestAsync(store, request).GetAwaiter().GetResult().Value;
        return (store, a, b, item, id);
    }

    private static UpdateTransferRequestCommandHandler Handler(FakeInventoryStore store) =>
        new(store, NullLogger<UpdateTransferRequestCommandHandler>.Instance);

    private static UpdateTransferRequestCommand Update(Guid id, Guid source, Guid destination, Guid item, int quantity, Pancras.Domain.Employees.EmployeeActor actor) =>
        new() { TransferSlipId = id, SourceLocationId = source, DestinationLocationId = destination, Note = "mới", Actor = actor, Lines = [new TransferLineInput(item, quantity)] };

    [Fact]
    public async Task Update_ChangesQuantityAndNote_WhileUnapproved()
    {
        var (store, a, b, item, id) = Setup();

        var result = await Handler(store).HandleAsync(Update(id, a.Id, b.Id, item, 7, TestActors.Keeper(b.Id)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, store.Transfers[0].Lines.Single().Quantity);
        Assert.Equal("mới", store.Transfers[0].Note);
        Assert.Equal(TransferStatus.Requested, store.Transfers[0].Status);
    }

    [Fact]
    public async Task Update_IsRejectedAfterApproval_ByOutsiders_AndForBadData()
    {
        var (store, a, b, item, id) = Setup();
        var outsider = await Handler(store).HandleAsync(Update(id, a.Id, b.Id, item, 3, TestActors.Keeper(Guid.NewGuid())), CancellationToken.None);
        var sameLocation = await Handler(store).HandleAsync(Update(id, a.Id, a.Id, item, 3, TestActors.Admin), CancellationToken.None);
        var zero = await Handler(store).HandleAsync(Update(id, a.Id, b.Id, item, 0, TestActors.Admin), CancellationToken.None);
        await new DecideTransferCommandHandler(store, NullLogger<DecideTransferCommandHandler>.Instance)
            .HandleAsync(new DecideTransferCommand(id, TestActors.Admin, true), CancellationToken.None);

        var afterApproval = await Handler(store).HandleAsync(Update(id, a.Id, b.Id, item, 3, TestActors.Admin), CancellationToken.None);

        Assert.Equal(StockError.Forbidden, outsider.StockError);
        Assert.Equal(StockError.InvalidTransfer, sameLocation.StockError);
        Assert.Equal(StockError.InvalidTransfer, zero.StockError);
        Assert.Equal(StockError.ApprovalRejected, afterApproval.StockError);
        Assert.Equal(2, store.Transfers[0].Lines.Single().Quantity);
    }

    [Fact]
    public async Task Reject_WithoutAReason_IsRefused()
    {
        var (store, _, _, _, id) = Setup();

        var result = await new DecideTransferCommandHandler(store, NullLogger<DecideTransferCommandHandler>.Instance)
            .HandleAsync(new DecideTransferCommand(id, TestActors.Admin, false, "  "), CancellationToken.None);

        Assert.Equal(StockError.ReasonRequired, result.StockError);
        Assert.Equal(TransferStatus.Requested, store.Transfers[0].Status);
    }
}
