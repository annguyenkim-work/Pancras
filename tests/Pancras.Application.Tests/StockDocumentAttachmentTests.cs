using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Common;
using Pancras.Application.Inventory;
using Pancras.Application.Purchasing;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

internal sealed class FakeStockDocumentAttachments : IStockDocumentAttachmentRepository
{
    public List<StockDocumentAttachment> Items { get; } = [];

    public Task<IReadOnlyList<StockDocumentAttachment>> ListAsync(StockDocumentKind kind, Guid documentId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockDocumentAttachment>>(Items.Where(item => item.Kind == kind && item.DocumentId == documentId).ToArray());

    public Task<StockDocumentAttachment?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<StockDocumentAttachment>>> ListForDocumentsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<StockDocumentAttachment>>>(
            Items.Where(item => documentIds.Contains(item.DocumentId)).GroupBy(item => item.DocumentId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<StockDocumentAttachment>)group.ToList()));

    public Task AddAsync(StockDocumentAttachment attachment, CancellationToken cancellationToken)
    {
        Items.Add(attachment);
        return Task.CompletedTask;
    }
}

internal sealed class FakeAttachmentStorage : IAttachmentStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

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

public class StockDocumentAttachmentTests
{
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A];

    private readonly FakeInventoryStore _store = new();
    private readonly FakeStockDocumentAttachments _attachments = new();
    private readonly FakeAttachmentStorage _storage = new();
    private readonly StockLocation _a = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
    private readonly StockLocation _b = StockLocation.Create("Kho B", StockLocationKind.Warehouse);
    private readonly Guid _item = Guid.CreateVersion7();

    public StockDocumentAttachmentTests()
    {
        _store.Locations.Add(_a);
        _store.Locations.Add(_b);
        _store.Balances.Add(StockBalance.Create(_item, _a.Id, 10, 0, 0));
    }

    private Guid NewStocktake()
    {
        var result = new CreateStocktakeCommandHandler(_store, NullLogger<CreateStocktakeCommandHandler>.Instance)
            .HandleAsync(
                new CreateStocktakeCommand
                {
                    Actor = TestActors.Keeper(_a.Id),
                    StockLocationId = _a.Id,
                    CatalogItemId = _item,
                    CountedQuantity = 8,
                    Reason = "Vỡ 2",
                    DocumentRef = "BB-01"
                },
                CancellationToken.None).GetAwaiter().GetResult();
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private UploadStockDocumentAttachmentCommandHandler Upload() =>
        new(_store, _attachments, _storage, NullLogger<UploadStockDocumentAttachmentCommandHandler>.Instance);

    private OpenStockDocumentAttachmentHandler Open() => new(_store, _attachments, _storage);

    private static UploadStockDocumentAttachmentCommand Command(Guid id, EmployeeActor actor, string name = "anh-hang.pdf", byte[]? content = null) =>
        new() { Kind = StockDocumentKind.Stocktake, DocumentId = id, FileName = name, Content = new MemoryStream(content ?? Pdf), Actor = actor };

    [Fact]
    public async Task Upload_StoresTheFile_ForSomeoneAtTheDocumentLocation()
    {
        var id = NewStocktake();

        var result = await Upload().HandleAsync(Command(id, TestActors.Keeper(_a.Id)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var saved = Assert.Single(_attachments.Items);
        Assert.Equal(StockDocumentKind.Stocktake, saved.Kind);
        Assert.Equal(id, saved.DocumentId);
        Assert.Equal("application/pdf", saved.ContentType);
        Assert.Single(_storage.Files);
    }

    [Fact]
    public async Task Upload_IsRefused_ForAnotherLocation_OrAnUnknownDocument_OrABadFile()
    {
        var id = NewStocktake();

        var outsider = await Upload().HandleAsync(Command(id, TestActors.Keeper(_b.Id)), CancellationToken.None);
        var sales = await Upload().HandleAsync(Command(id, TestActors.Sales(_a.Id)), CancellationToken.None);
        var unknown = await Upload().HandleAsync(Command(Guid.NewGuid(), TestActors.Admin), CancellationToken.None);
        var fake = await Upload().HandleAsync(Command(id, TestActors.Keeper(_a.Id), "x.pdf", [1, 2, 3, 4]), CancellationToken.None);
        var exe = await Upload().HandleAsync(Command(id, TestActors.Keeper(_a.Id), "x.exe"), CancellationToken.None);

        Assert.Equal(StockError.Forbidden, outsider.StockError);
        Assert.Equal(StockError.Forbidden, sales.StockError);
        Assert.Equal(StockError.Forbidden, unknown.StockError);
        Assert.Equal(StockError.AttachmentInvalid, fake.StockError);
        Assert.Equal(StockError.AttachmentInvalid, exe.StockError);
        Assert.Empty(_attachments.Items);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public void CheckFiles_RejectsTheWholeBatch_BeforeAnythingIsCreated()
    {
        var good = new PendingTransferFile("a.pdf", 100, Pdf);

        var ok = UploadStockDocumentAttachmentCommandHandler.CheckFiles([good]);
        var tooBig = UploadStockDocumentAttachmentCommandHandler.CheckFiles([good, new PendingTransferFile("b.pdf", 11L * 1024 * 1024, Pdf)]);
        var bad = UploadStockDocumentAttachmentCommandHandler.CheckFiles([good, new PendingTransferFile("c.pdf", 100, [1, 2, 3, 4])]);
        var none = UploadStockDocumentAttachmentCommandHandler.CheckFiles([]);

        Assert.True(ok.IsSuccess);
        Assert.Equal(StockError.AttachmentTooLarge, tooBig.StockError);
        Assert.Equal(StockError.AttachmentInvalid, bad.StockError);
        Assert.True(none.IsSuccess);
    }

    [Fact]
    public async Task Open_ReturnsTheFile_OnlyToSomeoneAtTheDocumentLocation()
    {
        var id = NewStocktake();
        var saved = (await Upload().HandleAsync(Command(id, TestActors.Keeper(_a.Id)), CancellationToken.None)).Value;

        var own = await Open().HandleAsync(saved, TestActors.Lead(_a.Id), CancellationToken.None);
        var finance = await Open().HandleAsync(saved, TestActors.Accountant, CancellationToken.None);
        var other = await Open().HandleAsync(saved, TestActors.Keeper(_b.Id), CancellationToken.None);

        Assert.True(own.IsSuccess);
        Assert.Equal("anh-hang.pdf", own.Value!.FileName);
        Assert.True(finance.IsSuccess);
        Assert.True(other.IsFailure);
    }

    [Fact]
    public async Task ListedDocuments_CarryTheirFiles()
    {
        var id = NewStocktake();
        await Upload().HandleAsync(Command(id, TestActors.Keeper(_a.Id)), CancellationToken.None);
        var list = new ListInventoryDocumentsHandler(_store, NullLogger<ListInventoryDocumentsHandler>.Instance, stockAttachments: _attachments);

        var rows = await list.ListStocktakesAsync(TestActors.Keeper(_a.Id), CancellationToken.None);

        var file = Assert.Single(Assert.Single(rows).Files!);
        Assert.Equal("anh-hang.pdf", file.FileName);
    }
}
