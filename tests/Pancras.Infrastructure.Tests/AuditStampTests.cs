using Microsoft.EntityFrameworkCore;
using Pancras.Application.Auditing;
using Pancras.Domain.Employees;
using Pancras.Domain.Suppliers;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Infrastructure.Tests;

/// <summary>Dấu vết tạo/sửa (mã nhân viên + giờ) phải được ghi tự động cho mọi module qua interceptor.</summary>
public class AuditStampTests(PostgresFixture fixture) : PostgresTest(fixture)
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

    private PancrasDbContext NewContext(string? actor, FakeClock clock) =>
        Database.NewContext(new AuditStampInterceptor(new FakeRequest(actor), clock));

    private static Supplier NewSupplier() =>
        Supplier.Create("NCC1", "Nhà cung cấp 1", "VN", "A", "0900000000", "a@example.com", "123");

    [Fact]
    public async Task Insert_StampsUpdaterCodeAndTime()
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
        Assert.Equal("AnNK", saved.UpdatedBy);
        Assert.Equal(at, saved.UpdatedAt);
    }

    [Fact]
    public async Task Update_OverwritesLastEditorCodeAndTime()
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
        Assert.Equal("BinhTT", saved.UpdatedBy);
        Assert.Equal(edited, saved.UpdatedAt);
    }

    [Fact]
    public async Task Update_AdvancesVersion_AndAStaleSaveIsRejected()
    {
        var at = new DateTimeOffset(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);
        var supplier = NewSupplier();
        await using (var context = NewContext("AnNK", new FakeClock(at)))
        {
            context.Add(supplier);
            await context.SaveChangesAsync();
        }

        await using var first = NewContext("A", new FakeClock(at));
        await using var second = NewContext("B", new FakeClock(at));
        var one = await first.Set<Supplier>().SingleAsync(s => s.Id == supplier.Id);
        var two = await second.Set<Supplier>().SingleAsync(s => s.Id == supplier.Id);
        Assert.Equal(0, one.Version);

        one.Update("NCC1", "Tên A", "VN", "A", "0900000000", "a@example.com", "123");
        await first.SaveChangesAsync();
        Assert.Equal(1, one.Version);

        two.Update("NCC1", "Tên B", "VN", "A", "0900000000", "a@example.com", "123");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task NoActor_IsStampedAsSystem()
    {
        var supplier = NewSupplier();
        await using var context = NewContext(null, new FakeClock(DateTimeOffset.UtcNow));
        context.Add(supplier);
        await context.SaveChangesAsync();
        Assert.Equal(AuditStampInterceptor.SystemActor, supplier.UpdatedBy);
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
        Assert.Equal(at, saved.UpdatedAt);
        Assert.Equal("Admin", saved.UpdatedBy);
    }
}
