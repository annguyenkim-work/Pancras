using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Domain.Purchasing;
using Pancras.Infrastructure.Persistence;

namespace Pancras.Web.Tests;

public class PurchaseModelTests : IClassFixture<PancrasWebFactory>
{
    private readonly PancrasWebFactory _factory;

    public PurchaseModelTests(PancrasWebFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Dòng, khoản chi phí và phần chia được tạo trong mã với Guid có sẵn rồi gắn vào đơn đã lưu. Nếu khóa là "do EF sinh",
    /// EF coi chúng là bản ghi đã tồn tại và gửi UPDATE (không khớp dòng nào), làm thêm/sửa đơn không lưu được.
    /// </summary>
    [Theory]
    [InlineData(typeof(PurchaseLine))]
    [InlineData(typeof(PurchaseCharge))]
    [InlineData(typeof(PurchaseChargeShare))]
    public void ChildKeysOfAnExistingOrder_AreAssignedByTheApplication(Type entity)
    {
        using var scope = _factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<PancrasDbContext>().Model;

        var key = model.FindEntityType(entity)!.FindPrimaryKey()!.Properties.Single();

        Assert.Equal(ValueGenerated.Never, key.ValueGenerated);
    }
}
