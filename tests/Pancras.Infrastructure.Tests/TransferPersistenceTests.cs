using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Pancras.Domain.Inventory;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Infrastructure.Tests;

/// <summary>Thay danh sách dòng của phiếu đã lưu phải INSERT dòng mới và xóa dòng cũ, không phải UPDATE dòng chưa tồn tại.</summary>
public class TransferPersistenceTests
{
    private readonly DbContextOptions<PancrasDbContext> _options = new DbContextOptionsBuilder<PancrasDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options;

    [Fact]
    public async Task UpdatingARequest_ReplacesItsLines()
    {
        var source = Guid.CreateVersion7();
        var destination = Guid.CreateVersion7();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        var slip = TransferSlip.Create(source, destination, "cũ", [(first, 2)])!;
        await using (var context = new PancrasDbContext(_options))
        {
            context.TransferSlips.Add(slip);
            await context.SaveChangesAsync();
        }

        await using (var context = new PancrasDbContext(_options))
        {
            var tracked = await context.TransferSlips.Include(item => item.Lines).SingleAsync(item => item.Id == slip.Id);
            Assert.True(tracked.TryUpdateRequest(source, destination, "mới", [(first, 5), (second, 1)]));
            await context.SaveChangesAsync();
        }

        await using var read = new PancrasDbContext(_options);
        var saved = await read.TransferSlips.AsNoTracking().Include(item => item.Lines).SingleAsync(item => item.Id == slip.Id);
        Assert.Equal("mới", saved.Note);
        Assert.Equal([5, 1], saved.Lines.OrderBy(line => line.Quantity == 5 ? 0 : 1).Select(line => line.Quantity).ToArray());
        Assert.Equal(2, await read.TransferLines.CountAsync());
    }
}
