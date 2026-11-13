using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pancras.Application.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Suppliers;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Infrastructure.Tests;

/// <summary>Dấu vết tạo/sửa (mã nhân viên + giờ) phải được ghi tự động cho mọi module qua interceptor.</summary>
public class AuditStampTests
{
    private sealed class FakeRequest(string? code) : IRequestInfo
    {
        public string? IpAddress => null;

        public Guid? ActorId => null;

        public string? ActorCode => code;
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly string _database = Guid.NewGuid().ToString("N");

    private PancrasDbContext NewContext(string? actor, FakeClock clock) =>
        new(new DbContextOptionsBuilder<PancrasDbContext>()
            .UseInMemoryDatabase(_database)
            .AddInterceptors(new AuditStampInterceptor(new FakeRequest(actor), clock))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static Supplier NewSupplier() =>
        Supplier.Create("NCC1", "Nhà cung cấp 1", "VN", "A", "0900000000", "a@example.com", "123");

    [Fact]
    public async Task Insert_StampsCreatorCodeAndTime_AndLeavesUpdateEmpty()
    {
        var at = new DateTimeOffset(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);
        var supplier = NewSupplier();
        await using (var context = NewContext("AnNK", new FakeClock(at)))
        {
            context.Add(supplier);
            await context.SaveChangesAsync();
        }

        await using var read = NewContext("x", new FakeClock(at));
        var saved = await read.Set<Supplier>().AsNoTracking().SingleAsync(s => s.Id == supplier.Id);
        Assert.Equal("AnNK", saved.CreatedBy);
        Assert.Equal(at, saved.CreatedAt);
        Assert.Null(saved.UpdatedAt);
        Assert.Equal("", saved.UpdatedBy);
    }

    [Fact]
    public async Task Update_StampsLastEditorCode_AndKeepsCreator()
    {
        var created = new DateTimeOffset(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);
        var edited = created.AddHours(3);
        var supplier = NewSupplier();
        await using (var context = NewContext("AnNK", new FakeClock(created)))
        {
            context.Add(supplier);
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext("BinhTT", new FakeClock(edited)))
        {
            var tracked = await context.Set<Supplier>().SingleAsync(s => s.Id == supplier.Id);
            tracked.Update("NCC1", "Tên mới", "VN", "A", "0900000000", "a@example.com", "123");
            await context.SaveChangesAsync();
        }

        await using var read = NewContext("x", new FakeClock(edited));
        var saved = await read.Set<Supplier>().AsNoTracking().SingleAsync(s => s.Id == supplier.Id);
        Assert.Equal("AnNK", saved.CreatedBy);
        Assert.Equal(created, saved.CreatedAt);
        Assert.Equal("BinhTT", saved.UpdatedBy);
        Assert.Equal(edited, saved.UpdatedAt);
    }

    [Fact]
    public async Task NoActor_IsStampedAsSystem()
    {
        var supplier = NewSupplier();
        await using var context = NewContext(null, new FakeClock(DateTimeOffset.UtcNow));
        context.Add(supplier);
        await context.SaveChangesAsync();
        Assert.Equal(AuditStampInterceptor.SystemActor, supplier.CreatedBy);
    }

    [Fact]
    public async Task FailedLoginCounter_DoesNotCountAsAnEmployeeEdit()
    {
        var at = new DateTimeOffset(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);
        var employee = Employee.Create("Nguyen A", "AnN", "ANN", "hash", "Sales");
        await using (var context = NewContext("Admin", new FakeClock(at)))
        {
            context.Add(employee);
            await context.SaveChangesAsync();
        }

        await using (var context = NewContext(null, new FakeClock(at.AddHours(1))))
        {
            var tracked = await context.Set<Employee>().SingleAsync(e => e.Id == employee.Id);
            tracked.RegisterFailedLogin(at.AddHours(1));
            await context.SaveChangesAsync();
        }

        await using var read = NewContext("x", new FakeClock(at));
        var saved = await read.Set<Employee>().AsNoTracking().SingleAsync(e => e.Id == employee.Id);
        Assert.Equal(1, saved.FailedLoginCount);
        Assert.Null(saved.UpdatedAt);
    }
}
