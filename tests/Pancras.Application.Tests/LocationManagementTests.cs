using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Locations;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Application.Tests;

public class LocationManagementTests
{
    private sealed class FakeManagement : ILocationManagementRepository
    {
        public List<StockLocation> Locations { get; } = [];

        public bool Stock { get; set; }

        public bool ActiveEmployees { get; set; }

        public bool OpenDocuments { get; set; }

        public int Saves { get; private set; }

        public Task<StockLocation?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Locations.FirstOrDefault(l => l.Id == id && !l.IsDeleted));

        public Task<bool> NameExistsAsync(string name, Guid excludingId, CancellationToken cancellationToken) =>
            Task.FromResult(Locations.Any(l => !l.IsDeleted && l.Id != excludingId && string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> HasStockAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Stock);

        public Task<bool> HasActiveEmployeesAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(ActiveEmployees);

        public Task<bool> HasOpenDocumentsAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(OpenDocuments);

        public Task<bool> TrySaveAsync(CancellationToken cancellationToken)
        {
            Saves++;
            return Task.FromResult(true);
        }
    }

    private static readonly EmployeeActor Admin = new(Guid.NewGuid(), EmployeeRole.Admin, null);

    private static (FakeManagement Repo, StockLocation Location) Setup()
    {
        var repo = new FakeManagement();
        var location = StockLocation.Create("Kho A", StockLocationKind.Warehouse);
        repo.Locations.Add(location);
        repo.Locations.Add(StockLocation.Create("Kho B", StockLocationKind.Warehouse));
        return (repo, location);
    }

    [Fact]
    public async Task Delete_IsBlocked_WhileOpenPurchaseOrdersOrTransfersReferenceTheLocation()
    {
        var (repo, location) = Setup();
        repo.OpenDocuments = true;

        var blocked = await Delete(repo).HandleAsync(location.Id, Admin, CancellationToken.None);
        repo.OpenDocuments = false;
        var allowed = await Delete(repo).HandleAsync(location.Id, Admin, CancellationToken.None);

        Assert.Equal(LocationManageError.HasOpenDocuments, blocked);
        Assert.Equal(LocationManageError.None, allowed);
        Assert.True(location.IsDeleted);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Update_RejectsAKindThatIsNotDefined(int kind)
    {
        var (repo, location) = Setup();

        var result = await Update(repo).HandleAsync(
            new UpdateLocationCommand { Id = location.Id, Name = "Kho A", Kind = (StockLocationKind)kind },
            Admin,
            CancellationToken.None);

        Assert.Equal(LocationManageError.InvalidKind, result);
    }

    [Fact]
    public async Task Delete_ByAnUnauthorisedRole_IsAuditedAsAccessDenied_AndDoesNothing()
    {
        var (repo, location) = Setup();
        var audit = new MemoryAudit();
        var handler = new DeleteLocationCommandHandler(repo, NullLogger<DeleteLocationCommandHandler>.Instance, audit: audit);

        var result = await handler.HandleAsync(location.Id, new EmployeeActor(Guid.NewGuid(), EmployeeRole.Sales, null), CancellationToken.None);

        Assert.Equal(LocationManageError.Forbidden, result);
        Assert.False(location.IsDeleted);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(Pancras.Domain.Auditing.AuditActions.AccessDenied, entry.Action);
    }

    private static UpdateLocationCommandHandler Update(FakeManagement repo) => new(repo, NullLogger<UpdateLocationCommandHandler>.Instance);

    private static DeleteLocationCommandHandler Delete(FakeManagement repo) => new(repo, NullLogger<DeleteLocationCommandHandler>.Instance);

    [Fact]
    public async Task Update_RenamesAndChangesKind()
    {
        var (repo, location) = Setup();
        var error = await Update(repo).HandleAsync(new UpdateLocationCommand { Id = location.Id, Name = " Showroom 1 ", Kind = StockLocationKind.Showroom }, Admin, CancellationToken.None);
        Assert.Equal(LocationManageError.None, error);
        Assert.Equal("Showroom 1", location.Name);
        Assert.Equal(StockLocationKind.Showroom, location.Kind);
    }

    [Fact]
    public async Task Update_RejectsDuplicateNameAndNonManagers()
    {
        var (repo, location) = Setup();
        Assert.Equal(LocationManageError.DuplicateName, await Update(repo).HandleAsync(new UpdateLocationCommand { Id = location.Id, Name = "kho b", Kind = StockLocationKind.Warehouse }, Admin, CancellationToken.None));
        var accountant = new EmployeeActor(Guid.NewGuid(), EmployeeRole.Accountant, null);
        Assert.Equal(LocationManageError.Forbidden, await Update(repo).HandleAsync(new UpdateLocationCommand { Id = location.Id, Name = "X", Kind = StockLocationKind.Warehouse }, accountant, CancellationToken.None));
        Assert.Equal(0, repo.Saves);
    }

    [Fact]
    public async Task Delete_IsSoft_AndKeepsTheRecord()
    {
        var (repo, location) = Setup();
        Assert.Equal(LocationManageError.None, await Delete(repo).HandleAsync(location.Id, Admin, CancellationToken.None));
        Assert.True(location.IsDeleted);
        Assert.Contains(location, repo.Locations);
    }

    [Fact]
    public async Task Delete_IsBlockedByStockOrActiveEmployees()
    {
        var (repo, location) = Setup();
        repo.Stock = true;
        Assert.Equal(LocationManageError.HasStock, await Delete(repo).HandleAsync(location.Id, Admin, CancellationToken.None));
        repo.Stock = false;
        repo.ActiveEmployees = true;
        Assert.Equal(LocationManageError.HasActiveEmployees, await Delete(repo).HandleAsync(location.Id, Admin, CancellationToken.None));
        Assert.False(location.IsDeleted);
    }
}
