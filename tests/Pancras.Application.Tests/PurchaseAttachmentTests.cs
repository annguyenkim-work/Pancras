using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Purchasing;
using Pancras.Domain.Auditing;
using Pancras.Domain.Purchasing;

namespace Pancras.Application.Tests;

internal sealed class MemoryAttachments : IPurchaseAttachmentRepository, IAttachmentStorage
{
    public List<PurchaseAttachment> Items { get; } = [];

    public Dictionary<string, byte[]> Files { get; } = [];

    public Task<IReadOnlyList<PurchaseAttachment>> ListAsync(Guid purchaseOrderId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PurchaseAttachment>>(Items.Where(item => item.PurchaseOrderId == purchaseOrderId).ToArray());

    public Task<PurchaseAttachment?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

    public Task AddAsync(PurchaseAttachment attachment, CancellationToken cancellationToken)
    {
        Items.Add(attachment);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        Items.RemoveAll(item => item.Id == id);
        return Task.CompletedTask;
    }

    public async Task SaveAsync(string storedName, Stream content, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        Files[storedName] = copy.ToArray();
    }

    public Task<Stream?> OpenReadAsync(string storedName, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(Files.TryGetValue(storedName, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string storedName, CancellationToken cancellationToken)
    {
        Files.Remove(storedName);
        return Task.CompletedTask;
    }
}

public class PurchaseAttachmentTests
{
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x0A];

    private readonly FakePurchaseStore _orders = new();
    private readonly MemoryAttachments _store = new();
    private readonly MemoryAudit _audit = new();
    private readonly PurchaseOrder _order;

    public PurchaseAttachmentTests()
    {
        _order = PurchaseOrder.TryCreate("PO-A", Guid.CreateVersion7(), Guid.CreateVersion7(), null, null, null, null, 25000m, 0m, [(Guid.CreateVersion7(), 1, 10m, 0m, 0m, 0m, 0m)])!;
        _orders.Orders.Add(_order);
    }

    private UploadPurchaseAttachmentCommandHandler Upload() =>
        new(_orders, _store, _store, new NoopUnitOfWork(), NullLogger<UploadPurchaseAttachmentCommandHandler>.Instance, null, _audit);

    private Task<Result<Guid, PurchaseError>> UploadAsync(string name, byte[] bytes, AttachmentKind kind = AttachmentKind.CustomsDeclaration) =>
        Upload().HandleAsync(
            new UploadPurchaseAttachmentCommand { Actor = TestActors.Accountant, PurchaseOrderId = _order.Id, Kind = kind, FileName = name, Content = new MemoryStream(bytes) },
            CancellationToken.None);

    [Fact]
    public async Task AValidPdf_IsStoredUnderASystemName_AndAudited()
    {
        var result = await UploadAsync(@"C:\fakepath\..\Tờ khai 01.pdf", Pdf);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(_store.Items);
        Assert.Equal("Tờ khai 01.pdf", item.FileName);
        Assert.Equal("application/pdf", item.ContentType);
        Assert.Equal(Pdf.Length, item.SizeBytes);
        Assert.DoesNotContain("khai", item.StoredName, StringComparison.Ordinal);
        Assert.True(AttachmentRules.IsSafeStoredName(item.StoredName));
        Assert.Equal(Pdf, _store.Files[item.StoredName]);
        Assert.Contains(_audit.Entries, entry => entry.Action == AuditActions.PurchaseAttachmentAdded);
    }

    [Theory]
    [InlineData("old.doc")]
    [InlineData("old.xls")]
    [InlineData("tool.exe")]
    [InlineData("page.html")]
    [InlineData("script.js")]
    [InlineData("noext")]
    [InlineData("   ")]
    public async Task OtherFileTypes_AreRefused(string name)
    {
        var result = await UploadAsync(name, Pdf);

        Assert.Equal(PurchaseError.AttachmentInvalid, result.Error);
        Assert.Empty(_store.Items);
        Assert.Empty(_store.Files);
    }

    [Fact]
    public async Task ARenamedProgram_IsRefused_BecauseTheContentDoesNotMatch()
    {
        var result = await UploadAsync("scan.pdf", [0x4D, 0x5A, 0x90, 0x00, 0x03]);

        Assert.Equal(PurchaseError.AttachmentInvalid, result.Error);
        Assert.Empty(_store.Files);
    }

    [Fact]
    public async Task EmptyAndOversizedFiles_AreRefused()
    {
        var empty = await UploadAsync("a.pdf", []);
        var big = new byte[AttachmentRules.MaxBytes + 1];
        Pdf.CopyTo(big, 0);
        var tooLarge = await UploadAsync("b.pdf", big);

        Assert.Equal(PurchaseError.AttachmentInvalid, empty.Error);
        Assert.Equal(PurchaseError.AttachmentTooLarge, tooLarge.Error);
        Assert.Empty(_store.Files);
    }

    [Fact]
    public async Task AnOrderHoldsAtMostThirtyFiles()
    {
        for (var index = 0; index < AttachmentRules.MaxPerOrder; index++)
        {
            Assert.True((await UploadAsync($"f{index}.pdf", Pdf)).IsSuccess);
        }

        var extra = await UploadAsync("extra.pdf", Pdf);

        Assert.Equal(PurchaseError.AttachmentLimit, extra.Error);
    }

    [Fact]
    public async Task AnUnknownOrder_IsRefused()
    {
        var result = await Upload().HandleAsync(
            new UploadPurchaseAttachmentCommand { Actor = TestActors.Accountant, PurchaseOrderId = Guid.CreateVersion7(), Kind = AttachmentKind.Other, FileName = "a.pdf", Content = new MemoryStream(Pdf) },
            CancellationToken.None);

        Assert.Equal(PurchaseError.InvalidOrder, result.Error);
    }

    [Fact]
    public async Task Opening_ReturnsTheContentAndRecordsWhoLooked()
    {
        var id = (await UploadAsync("a.pdf", Pdf)).Value;

        var opened = await new OpenPurchaseAttachmentHandler(_orders, _store, _store, NullLogger<OpenPurchaseAttachmentHandler>.Instance, null, _audit)
            .HandleAsync(id, TestActors.Accountant, CancellationToken.None);
        var missing = await new OpenPurchaseAttachmentHandler(_orders, _store, _store, NullLogger<OpenPurchaseAttachmentHandler>.Instance)
            .HandleAsync(Guid.CreateVersion7(), TestActors.Accountant, CancellationToken.None);

        Assert.True(opened.IsSuccess);
        using var stream = new MemoryStream();
        await opened.Value!.Content.CopyToAsync(stream);
        Assert.Equal(Pdf, stream.ToArray());
        Assert.Equal("a.pdf", opened.Value.FileName);
        Assert.Contains(_audit.Entries, entry => entry.Action == AuditActions.PurchaseAttachmentDownloaded);
        Assert.Equal(PurchaseError.AttachmentNotFound, missing.Error);
    }

    [Fact]
    public async Task Opening_ByWarehouseStaff_IsAllowedOnlyForVisibleOrdersOfTheirOwnLocation()
    {
        var id = (await UploadAsync("a.pdf", Pdf)).Value;
        var handler = new OpenPurchaseAttachmentHandler(_orders, _store, _store, NullLogger<OpenPurchaseAttachmentHandler>.Instance);
        var mine = TestActors.Keeper(_order.ReceiveLocationId);
        var elsewhere = TestActors.Keeper(Guid.CreateVersion7());

        var whileDraft = await handler.HandleAsync(id, mine, CancellationToken.None);
        _order.Approve();
        var approvedMine = await handler.HandleAsync(id, mine, CancellationToken.None);
        var approvedElsewhere = await handler.HandleAsync(id, elsewhere, CancellationToken.None);

        Assert.Equal(PurchaseError.AttachmentNotFound, whileDraft.Error);
        Assert.True(approvedMine.IsSuccess);
        Assert.Equal(PurchaseError.AttachmentNotFound, approvedElsewhere.Error);
    }

    [Fact]
    public async Task Deleting_IsAllowedOnlyWhileTheOrderIsOpen()
    {
        var first = (await UploadAsync("a.pdf", Pdf)).Value;
        var delete = new DeletePurchaseAttachmentCommandHandler(_orders, _store, _store, new NoopUnitOfWork(), NullLogger<DeletePurchaseAttachmentCommandHandler>.Instance, null, _audit);
        var removed = await delete.HandleAsync(first, TestActors.Accountant, CancellationToken.None);
        var second = (await UploadAsync("b.pdf", Pdf)).Value;
        _order.ReceiveAll();
        var locked = await delete.HandleAsync(second, TestActors.Accountant, CancellationToken.None);

        Assert.True(removed.IsSuccess);
        Assert.Equal(PurchaseError.AlreadyReceived, locked.Error);
        Assert.Equal(second, Assert.Single(_store.Items).Id);
        Assert.Single(_store.Files);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("0123456789abcdef0123456789abcdef.exe")]
    [InlineData("short.pdf")]
    [InlineData("0123456789abcdef0123456789abcdeg.pdf")]
    [InlineData("0123456789abcdef0123456789abcdef/../a.pdf")]
    public void StoredNames_ThatCouldEscapeTheFolder_AreNotSafe(string name) =>
        Assert.False(AttachmentRules.IsSafeStoredName(name));
}
