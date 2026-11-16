using Microsoft.Extensions.Logging.Abstractions;
using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Domain.Catalog;

namespace Pancras.Application.Tests;

public class ListMatchingCatalogItemsHandlerTests
{
    [Fact]
    public async Task ExportSet_HasTheFourFilteredRows_AndNoCombo()
    {
        var repository = new FakeCatalogRepository();
        for (var index = 1; index <= 4; index++)
        {
            repository.Items.Add(ListCatalogItemsHandlerTests.Item("SP" + index, "Hàng " + index, ProductCategory.SanitaryEquipment));
        }

        repository.Items.Add(ListCatalogItemsHandlerTests.Item("NT01", "Tủ", ProductCategory.BathroomFurniture));
        repository.Items.Add(CatalogItem.CreateCombo("CB01", "Combo", ProductCategory.SanitaryEquipment, "Toto", null, "Bộ", null, null, null, null, 1m, 1m, 1m, 1m, 1m));
        var handler = new ListMatchingCatalogItemsHandler(repository, NullLogger<ListMatchingCatalogItemsHandler>.Instance);

        var rows = await handler.HandleAsync(ListCatalogItemsQuery.From(ProductCategoryToken.Sanitary, null), CancellationToken.None);
        var empty = await handler.HandleAsync(ListCatalogItemsQuery.From(ProductCategoryToken.Building, null), CancellationToken.None);

        Assert.Equal(4, rows.Count);
        Assert.DoesNotContain(rows, row => row.Code == "CB01" || row.Code == "NT01");
        Assert.Empty(empty);
    }
}
