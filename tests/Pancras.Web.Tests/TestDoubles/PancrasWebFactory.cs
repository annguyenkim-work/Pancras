using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pancras.Application.Catalog;
using Pancras.Application.Employees;
using Pancras.Application.Inventory;
using Pancras.Application.Purchasing;
using Pancras.Application.Suppliers;
using Pancras.Domain.Employees;
using Pancras.Domain.Suppliers;

namespace Pancras.Web.Tests;

// Máy chủ test dùng chung cho mọi test trang và các test double của nhân viên, nhà cung cấp, địa điểm, nhật ký, giao dịch.

public sealed class PancrasWebFactory : WebApplicationFactory<Program>
{
    public string LoginPerMinute { get; init; } = "100000";

    public string SensitivePerMinute { get; init; } = "100000";

    public TimeProvider? Clock { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("RateLimiting:LoginPerMinute", LoginPerMinute);
        builder.UseSetting("RateLimiting:SensitivePerMinute", SensitivePerMinute);
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Host=localhost;Port=5432;Database=pancras;Username=postgres;Password=testing");
        builder.ConfigureTestServices(services =>
        {
            if (Clock is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(Clock);
            }

            services.RemoveAll<Pancras.Application.Common.IUnitOfWork>();
            services.AddSingleton<Pancras.Application.Common.IUnitOfWork, WebTestUnitOfWork>();
            services.RemoveAll<Pancras.Application.Auditing.IAuditLog>();
            services.AddSingleton<Pancras.Application.Auditing.IAuditLog, NoAuditLog>();
            services.RemoveAll<IEmployeeRepository>();
            services.RemoveAll<IPasswordHasher>();
            services.AddSingleton<FakePasswordHasher>();
            services.AddSingleton<IPasswordHasher>(provider => provider.GetRequiredService<FakePasswordHasher>());
            services.AddSingleton<FakeEmployeeRepository>();
            services.AddSingleton<IEmployeeRepository>(provider => provider.GetRequiredService<FakeEmployeeRepository>());
            services.RemoveAll<Pancras.Application.Locations.ILocationRepository>();
            services.AddSingleton<LocationDirectoryDouble>();
            services.AddSingleton<Pancras.Application.Locations.ILocationRepository>(provider => provider.GetRequiredService<LocationDirectoryDouble>());
            services.RemoveAll<ISupplierRepository>();
            services.AddSingleton<SupplierDirectoryDouble>();
            services.AddSingleton<ISupplierRepository>(provider => provider.GetRequiredService<SupplierDirectoryDouble>());
            services.RemoveAll<ICatalogItemRepository>();
            services.AddSingleton<CatalogDirectoryDouble>();
            services.AddSingleton<ICatalogItemRepository>(provider => provider.GetRequiredService<CatalogDirectoryDouble>());
            services.RemoveAll<IInventoryRepository>();
            services.AddSingleton<InventoryDirectoryDouble>();
            services.AddSingleton<IInventoryRepository>(provider => provider.GetRequiredService<InventoryDirectoryDouble>());
            services.RemoveAll<Pancras.Application.Inventory.IStockDocumentAttachmentRepository>();
            services.AddSingleton<Pancras.Application.Inventory.IStockDocumentAttachmentRepository, StockDocumentAttachmentDouble>();
            services.RemoveAll<Pancras.Application.Inventory.ITransferAttachmentRepository>();
            services.AddSingleton<Pancras.Application.Inventory.ITransferAttachmentRepository, TransferAttachmentDouble>();
            services.RemoveAll<IPurchaseOrderRepository>();
            services.AddSingleton<PurchaseDirectoryDouble>();
            services.AddSingleton<IPurchaseOrderRepository>(provider => provider.GetRequiredService<PurchaseDirectoryDouble>());
            services.RemoveAll<Pancras.Application.Notifications.INoticeStore>();
            services.AddSingleton<Pancras.Application.Notifications.INoticeStore, NoticeStoreDouble>();
            services.RemoveAll<ISupplierPurchaseStatsReader>();
            services.AddSingleton<SupplierStatsDouble>();
            services.AddSingleton<ISupplierPurchaseStatsReader>(provider => provider.GetRequiredService<SupplierStatsDouble>());
            services.RemoveAll<IPurchaseAttachmentRepository>();
            services.RemoveAll<IAttachmentStorage>();
            services.AddSingleton<AttachmentDouble>();
            services.AddSingleton<IPurchaseAttachmentRepository>(provider => provider.GetRequiredService<AttachmentDouble>());
            services.AddSingleton<IAttachmentStorage>(provider => provider.GetRequiredService<AttachmentDouble>());
        });
    }
}

internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _utc = start;

    public void Advance(TimeSpan by) => _utc += by;

    public override DateTimeOffset GetUtcNow() => _utc;
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hash:" + password;

    public bool Verify(string password, string passwordHash) => passwordHash == "hash:" + password;
}

internal sealed class FakeEmployeeRepository : IEmployeeRepository
{
    public List<Employee> Employees { get; } = [];

    public Task<Employee?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(Employees.FirstOrDefault(employee => employee.NormalizedCode == normalizedCode));

    public Task<Employee?> GetByNormalizedCodeForUpdateAsync(string normalizedCode, CancellationToken cancellationToken) =>
        GetByNormalizedCodeAsync(normalizedCode, cancellationToken);

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(Employees.Any(employee => employee.NormalizedCode == normalizedCode));

    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Employees.FirstOrDefault(employee => employee.Id == id));

    public Task<Employee?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Employee>> ListReadOnlyAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Employee>>(Employees.ToArray());

    public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Employees.Count(employee => employee.Role == EmployeeRole.Admin && employee.IsActive));

    public Task<bool> ActiveLeadExistsAsync(Guid stockLocationId, Guid? excludeEmployeeId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task AddAsync(Employee employee, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task LockPersonnelRulesAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

internal sealed class SupplierStatsDouble : ISupplierPurchaseStatsReader
{
    public Dictionary<Guid, SupplierPurchaseStats> Stats { get; } = [];

    public Task<IReadOnlyDictionary<Guid, SupplierPurchaseStats>> ReadAsync(IReadOnlyCollection<Guid> supplierIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, SupplierPurchaseStats>>(Stats);
}

internal sealed class SupplierDirectoryDouble : ISupplierRepository
{
    public List<Supplier> Suppliers { get; } = [];

    public Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Supplier>>(Filtered(ListSuppliersQuery.From(null)));

    public Task<SupplierSlice> ListFilteredAsync(ListSuppliersQuery filter, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var ordered = Filtered(filter);
        var size = pageSize < 1 ? Pancras.Application.Common.PageSizes.Default : pageSize;
        var totalPages = ordered.Count == 0 ? 1 : (ordered.Count + size - 1) / size;
        var number = Math.Clamp(pageNumber < 1 ? 1 : pageNumber, 1, totalPages);
        IReadOnlyList<Supplier> items = ordered.Skip((number - 1) * size).Take(size).ToList();
        return Task.FromResult(new SupplierSlice(items, ordered.Count, number));
    }

    public Task<IReadOnlyList<string>> ListCountriesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(Suppliers.Select(supplier => supplier.Country).Distinct().Order().ToList());

    public Task<Supplier?> GetActiveReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.FirstOrDefault(supplier => supplier.Id == id && supplier.IsActive));

    public Task<Supplier?> GetAnyReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.FirstOrDefault(supplier => supplier.Id == id));

    public Task<Supplier?> GetActiveForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetActiveReadOnlyAsync(id, cancellationToken);

    public Task<Supplier?> GetAnyForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetAnyReadOnlyAsync(id, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(
            Suppliers.Where(supplier => ids.Contains(supplier.Id)).ToDictionary(supplier => supplier.Id, supplier => supplier.Name));

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCode, Guid? excludingId, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.Any(supplier =>
            supplier.IsActive && supplier.NormalizedCode == normalizedCode && supplier.Id != excludingId));

    public Task<string?> InactiveNameWithCodeAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.FirstOrDefault(supplier => !supplier.IsActive && supplier.NormalizedCode == normalizedCode)?.Name);

    public Task<int> CountActiveBeforeAsync(string name, string code, CancellationToken cancellationToken) =>
        Task.FromResult(Suppliers.Count(supplier =>
            supplier.IsActive
            && (string.CompareOrdinal(supplier.Name, name) < 0
                || (supplier.Name == name && string.CompareOrdinal(supplier.Code, code) < 0))));

    public Task AddAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        Suppliers.Add(supplier);
        return Task.CompletedTask;
    }

    private List<Supplier> Filtered(ListSuppliersQuery filter)
    {
        IEnumerable<Supplier> suppliers = filter.Status switch
        {
            SupplierStatusFilter.Inactive => Suppliers.Where(supplier => !supplier.IsActive),
            SupplierStatusFilter.All => Suppliers,
            _ => Suppliers.Where(supplier => supplier.IsActive)
        };
        if (filter.Country is not null)
        {
            suppliers = suppliers.Where(supplier => supplier.Country == filter.Country);
        }

        if (filter.Term is not null)
        {
            var needle = filter.Term;
            suppliers = suppliers.Where(supplier =>
                supplier.Code.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || supplier.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        return suppliers.OrderBy(supplier => supplier.Name).ThenBy(supplier => supplier.Code).ToList();
    }
}

internal sealed class LocationDirectoryDouble : Pancras.Application.Locations.ILocationRepository
{
    public List<Pancras.Domain.Inventory.StockLocation> Locations { get; } = [];

    public Task<IReadOnlyList<Pancras.Domain.Inventory.StockLocation>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Pancras.Domain.Inventory.StockLocation>>(Locations.ToArray());

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Locations.Any(location => location.Id == id));

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(Locations.Any(location => string.Equals(location.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(Pancras.Domain.Inventory.StockLocation location, CancellationToken cancellationToken)
    {
        Locations.Add(location);
        return Task.CompletedTask;
    }
}

internal sealed class NoAuditLog : Pancras.Application.Auditing.IAuditLog
{
    public List<Pancras.Domain.Auditing.AuditEntry> Entries { get; } = [];

    public void Stage(Pancras.Domain.Auditing.AuditEntry entry)
    {
        lock (Entries)
        {
            Entries.Add(entry);
        }
    }

    public Task RecordAsync(Pancras.Domain.Auditing.AuditEntry entry, CancellationToken cancellationToken)
    {
        lock (Entries)
        {
            Entries.Add(entry);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Pancras.Domain.Auditing.AuditEntry>> ListPageAsync(Pancras.Application.Auditing.AuditQuery query, Pancras.Application.Auditing.AuditCursor? cursor, bool backward, int limit, bool newestFirst, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Pancras.Domain.Auditing.AuditEntry>>([]);
}

/// <summary>UnitOfWork giả cho test trang: các test double giữ dữ liệu trong bộ nhớ nên commit luôn thành công.</summary>
internal sealed class WebTestUnitOfWork : Pancras.Application.Common.IUnitOfWork
{
    public Task<Pancras.Application.Common.IUnitOfWorkScope> BeginAsync(CancellationToken cancellationToken) =>
        Task.FromResult<Pancras.Application.Common.IUnitOfWorkScope>(new Scope());

    private sealed class Scope : Pancras.Application.Common.IUnitOfWorkScope
    {
        public Task<Pancras.Application.Common.SaveOutcome> CommitAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Pancras.Application.Common.SaveOutcome.Saved);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
