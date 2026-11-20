using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Pancras.Domain.Catalog;
using Pancras.Domain.Inventory;
using Pancras.Domain.Suppliers;
using Pancras.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Pancras.Infrastructure.Tests;

/// <summary>
/// PostgreSQL thật cho test hạ tầng (docs/02-coding-rules/testing.md mục 4). Một máy chủ cho cả lần chạy: migration áp một lần
/// vào database mẫu, mỗi test tạo database riêng từ mẫu đó. Mặc định khởi động container bằng Testcontainers (cần Docker);
/// đặt biến môi trường <see cref="ExternalServerVariable"/> để dùng một máy chủ có sẵn (chỉ tạo/xóa database tên <c>pancras_test_*</c>).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ExternalServerVariable = "PANCRAS_TEST_POSTGRES";

    // Cùng dòng với production (Ubuntu 24.04 LTS cài PostgreSQL 16).
    private const string Image = "postgres:16";

    private const string DatabasePrefix = "pancras_test_";

    private PostgreSqlContainer? _container;
    private string _adminConnection = string.Empty;
    private string _template = string.Empty;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable(ExternalServerVariable);
        if (string.IsNullOrWhiteSpace(external))
        {
            _container = new PostgreSqlBuilder(Image).Build();
            await _container.StartAsync();
            _adminConnection = _container.GetConnectionString();
        }
        else
        {
            _adminConnection = external;
        }

        _adminConnection = WithoutPooling(_adminConnection);
        _template = DatabasePrefix + "template_" + Guid.NewGuid().ToString("N");
        await ExecuteAsync("CREATE DATABASE \"" + _template + "\"");
        try
        {
            await using var context = new PancrasDbContext(Options(For(_template)));
            await context.Database.MigrateAsync();
        }
        catch
        {
            await ExecuteAsync("DROP DATABASE IF EXISTS \"" + _template + "\" WITH (FORCE)");
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        if (_template.Length > 0)
        {
            await ExecuteAsync("DROP DATABASE IF EXISTS \"" + _template + "\" WITH (FORCE)");
        }
    }

    /// <summary>Database mới, đã có đủ bảng theo migration, dành riêng cho một test.</summary>
    public async Task<PostgresDatabase> CreateDatabaseAsync()
    {
        var name = DatabasePrefix + Guid.NewGuid().ToString("N");
        await ExecuteAsync("CREATE DATABASE \"" + name + "\" TEMPLATE \"" + _template + "\"");
        return new PostgresDatabase(For(name), () => ExecuteAsync("DROP DATABASE IF EXISTS \"" + name + "\" WITH (FORCE)"));
    }

    private string For(string database) =>
        new NpgsqlConnectionStringBuilder(_adminConnection) { Database = database }.ConnectionString;

    private static string WithoutPooling(string connection) =>
        new NpgsqlConnectionStringBuilder(connection) { Pooling = false }.ConnectionString;

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    internal static DbContextOptions<PancrasDbContext> Options(string connection, params IInterceptor[] interceptors) =>
        new DbContextOptionsBuilder<PancrasDbContext>()
            .UseNpgsql(connection)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptors)
            .Options;
}

/// <summary>Một database riêng của một test; xóa khi test xong.</summary>
public sealed class PostgresDatabase(string connection, Func<Task> drop)
{
    public string ConnectionString { get; } = connection;

    public DbContextOptions<PancrasDbContext> Options(params IInterceptor[] interceptors) =>
        PostgresFixture.Options(ConnectionString, interceptors);

    public PancrasDbContext NewContext(params IInterceptor[] interceptors) => new(Options(interceptors));

    public Task DropAsync() => drop();
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}

/// <summary>Lớp nền cho test cần database: mỗi test một database riêng (<see cref="Database"/>), xóa sau test.</summary>
[Collection(PostgresCollection.Name)]
public abstract class PostgresTest(PostgresFixture fixture) : IAsyncLifetime
{
    protected PostgresDatabase Database { get; private set; } = null!;

    protected DbContextOptions<PancrasDbContext> Options => Database.Options();

    /// <summary>Lưu mọi thay đổi đang chờ của <paramref name="context"/> trong một scope IUnitOfWork (giống một handler).</summary>
    protected static async Task<Pancras.Application.Common.SaveOutcome> CommitAsync(PancrasDbContext context)
    {
        await using var scope = await new EfUnitOfWork(context, Microsoft.Extensions.Logging.Abstractions.NullLogger<EfUnitOfWork>.Instance).BeginAsync(CancellationToken.None);
        return await scope.CommitAsync(CancellationToken.None);
    }

    public virtual async Task InitializeAsync() => Database = await fixture.CreateDatabaseAsync();

    public async Task DisposeAsync() => await Database.DropAsync();

    // PostgreSQL kiểm khóa ngoại thật, nên dữ liệu cha (kho, mặt hàng, nhà cung cấp) phải có trước khi lưu phiếu/đơn.
    protected async Task<Guid> SeedLocationAsync(string name = "Kho test")
    {
        var location = StockLocation.Create(name, StockLocationKind.Warehouse);
        await using var context = Database.NewContext();
        context.StockLocations.Add(location);
        await context.SaveChangesAsync();
        return location.Id;
    }

    protected async Task<Guid[]> SeedItemsAsync(int count)
    {
        var items = Enumerable.Range(0, count)
            .Select(_ =>
            {
                var code = "SP" + Guid.NewGuid().ToString("N")[..8];
                return CatalogItem.Create(code, code, ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", 1m, null, null, null, 1m, 1m, 1m, 1m, 1m);
            })
            .ToArray();
        await using var context = Database.NewContext();
        context.CatalogItems.AddRange(items);
        await context.SaveChangesAsync();
        return items.Select(item => item.Id).ToArray();
    }

    protected async Task<Guid> SeedSupplierAsync()
    {
        var supplier = Supplier.Create("NCC" + Guid.NewGuid().ToString("N")[..6], "Nhà cung cấp", "VN", "A", "0900000000", "a@example.com", "123");
        await using var context = Database.NewContext();
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();
        return supplier.Id;
    }
}
