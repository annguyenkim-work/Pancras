using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Inventory;
using Pancras.Domain.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

/// <summary>
/// Lập và ghi thẳng biên bản kiểm kê, phiếu điều chỉnh, phiếu hủy đều là thao tác ghi dữ liệu nên phải có nhật ký
/// (notifications-and-audit.md mục B2). Lỗi B1 đợt test 2026-11-21: Admin/GĐ/KT ghi thẳng kiểm kê, điều chỉnh không có dòng nhật ký.
/// </summary>
public class StockDocumentAuditTests
{
    private readonly FakeInventoryStore _store = new();
    private readonly MemoryAudit _audit = new();
    private readonly StockLocation _location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
    private readonly Guid _itemId = Guid.CreateVersion7();

    public StockDocumentAuditTests()
    {
        _store.Locations.Add(_location);
        _store.Balances.Add(StockBalance.Create(_itemId, _location.Id, 10, 0, 0));
    }

    [Fact]
    public async Task Stocktake_PostedDirectlyByFinance_IsAuditedAsApproved()
    {
        var result = await StocktakeHandler().HandleAsync(Stocktake(TestActors.Accountant), CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertSingleEntry(AuditActions.StocktakeApproved, "stocktake", result.Value);
    }

    [Fact]
    public async Task Stocktake_DraftedByKeeper_IsAuditedAsRequested()
    {
        var result = await StocktakeHandler().HandleAsync(Stocktake(TestActors.Keeper(_location.Id)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertSingleEntry(AuditActions.StocktakeRequested, "stocktake", result.Value);
    }

    [Fact]
    public async Task Adjustment_PostedDirectlyByFinance_IsAuditedAsApproved()
    {
        var result = await AdjustmentHandler().HandleAsync(Adjustment(TestActors.Admin), CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertSingleEntry(AuditActions.AdjustmentApproved, "adjustment", result.Value);
    }

    [Fact]
    public async Task Adjustment_DraftedByKeeper_IsAuditedAsRequested()
    {
        var result = await AdjustmentHandler().HandleAsync(Adjustment(TestActors.Keeper(_location.Id)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertSingleEntry(AuditActions.AdjustmentRequested, "adjustment", result.Value);
    }

    [Fact]
    public async Task Disposal_DraftedByKeeper_IsAuditedAsRequested()
    {
        var result = await DisposalHandler().HandleAsync(Disposal(TestActors.Keeper(_location.Id)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertSingleEntry(AuditActions.DisposalRequested, "disposal", result.Value);
    }

    [Fact]
    public async Task Disposal_PostedDirectlyByFinance_StaysAuditedAsApproved()
    {
        var result = await DisposalHandler().HandleAsync(Disposal(TestActors.Director), CancellationToken.None);

        Assert.True(result.IsSuccess);
        AssertSingleEntry(AuditActions.DisposalApproved, "disposal", result.Value);
    }

    [Fact]
    public async Task RejectedDocuments_LeaveNoAuditEntry()
    {
        var stocktake = Stocktake(TestActors.Accountant);
        stocktake.Reason = " ";
        var adjustment = Adjustment(TestActors.Admin);
        adjustment.DocumentRef = null;
        var disposal = Disposal(TestActors.Keeper(_location.Id));
        disposal.Reason = null;

        Assert.True((await StocktakeHandler().HandleAsync(stocktake, CancellationToken.None)).IsFailure);
        Assert.True((await AdjustmentHandler().HandleAsync(adjustment, CancellationToken.None)).IsFailure);
        Assert.True((await DisposalHandler().HandleAsync(disposal, CancellationToken.None)).IsFailure);
        Assert.Empty(_audit.Entries);
    }

    private void AssertSingleEntry(string action, string targetType, Guid documentId)
    {
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(action, entry.Action);
        Assert.Equal(targetType, entry.TargetType);
        Assert.Equal(documentId.ToString("D"), entry.TargetId);
    }

    private CreateStocktakeCommandHandler StocktakeHandler() =>
        new(_store, _store, NullLogger<CreateStocktakeCommandHandler>.Instance, employees: new FakeEmployeeRepository(), audit: _audit);

    private CreateQuantityAdjustmentCommandHandler AdjustmentHandler() =>
        new(_store, _store, new FakeEmployeeRepository(), NullLogger<CreateQuantityAdjustmentCommandHandler>.Instance, audit: _audit);

    private CreateDisposalSlipCommandHandler DisposalHandler() =>
        new(
            _store,
            _store,
            new ApproveDisposalSlipCommandHandler(_store, _store, new FakeEmployeeRepository(), NullLogger<ApproveDisposalSlipCommandHandler>.Instance, audit: _audit),
            NullLogger<CreateDisposalSlipCommandHandler>.Instance,
            audit: _audit);

    private CreateStocktakeCommand Stocktake(EmployeeActor actor) =>
        new()
        {
            Actor = actor,
            StockLocationId = _location.Id,
            CatalogItemId = _itemId,
            CountedQuantity = 9,
            Reason = "Đếm thiếu",
            DocumentRef = "BBKK-01"
        };

    private CreateQuantityAdjustmentCommand Adjustment(EmployeeActor actor) =>
        new()
        {
            Actor = actor,
            StockLocationId = _location.Id,
            CatalogItemId = _itemId,
            Delta = 1,
            Reason = "Tìm lại được hàng",
            DocumentRef = "DC-01"
        };

    private CreateDisposalSlipCommand Disposal(EmployeeActor actor) =>
        new()
        {
            Actor = actor,
            StockLocationId = _location.Id,
            CatalogItemId = _itemId,
            Quantity = 1,
            Reason = "Vỡ",
            DocumentRef = "HY-01"
        };
}
