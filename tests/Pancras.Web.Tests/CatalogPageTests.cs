using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Application.Catalog;
using Pancras.Application.Employees;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;
using Pancras.Domain.Inventory;

namespace Pancras.Web.Tests;

public class CatalogPageTests : IClassFixture<PancrasWebFactory>
{
    private readonly PancrasWebFactory _factory;

    public CatalogPageTests(PancrasWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousCatalog_RedirectsToLogin()
    {
        using var client = _factory.CreateClient(NoRedirect);

        var response = await client.GetAsync("/catalog");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_ShowsVatNotice_FormatsMoney_AndKeepsMeasuresPlain()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var repository = Reset();
        var item = CatalogItem.Create("SP01", "Vòi lavabo", ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", 12m, 0.4m, 6, 1m, 0m, 15000000m, 12000000m, 11000000m, 10000000m);
        repository.Items.Add(item);
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var page = await client.GetAsync("/catalog");
        var html = await page.Content.ReadAsStringAsync();
        var detail = await client.GetAsync("/catalog?detail=" + item.Id.ToString("D"));
        var detailHtml = await detail.Content.ReadAsStringAsync();
        var add = await client.GetAsync("/catalog?add=true");
        var addHtml = await add.Content.ReadAsStringAsync();
        var prices = await client.GetAsync("/catalog?prices=" + item.Id.ToString("D"));
        var priceHtml = await prices.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var text = Decode(html);
        var detailText = Decode(detailHtml);
        var addText = Decode(addHtml);
        var priceText = Decode(priceHtml);
        Assert.Contains("15.000.000 VNĐ", text, StringComparison.Ordinal);
        Assert.Contains("0 VNĐ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("15.000.000 đ", text, StringComparison.Ordinal);
        Assert.Contains("0,4", detailText, StringComparison.Ordinal);
        Assert.DoesNotContain("0,4 VNĐ", detailText, StringComparison.Ordinal);
        Assert.DoesNotContain("12 VNĐ", detailText, StringComparison.Ordinal);
        Assert.Contains("Giá đã gồm VAT.", addText, StringComparison.Ordinal);
        Assert.Contains("Giá đã gồm VAT.", priceText, StringComparison.Ordinal);
        Assert.Contains("Thêm sản phẩm vào danh mục", addText, StringComparison.Ordinal);
        Assert.Contains("Lưu sản phẩm", addText, StringComparison.Ordinal);
        Assert.Contains("Sửa sản phẩm", priceText, StringComparison.Ordinal);
        Assert.Contains("0 VNĐ", addText, StringComparison.Ordinal);
        Assert.Contains("<dialog id=\"catalog-dialog\"", addHtml, StringComparison.Ordinal);
        Assert.Contains("id=\"catalog-dialog\" data-autoshow", addHtml, StringComparison.Ordinal);
        Assert.Contains("Chiều cao (mm)", addText, StringComparison.Ordinal);
        Assert.Contains("Chiều dài (mm)", addText, StringComparison.Ordinal);
        Assert.Contains("Chiều rộng (mm)", addText, StringComparison.Ordinal);
        Assert.Contains("Ảnh sản phẩm", addText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_WritesTheFourFilteredRows()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var repository = Reset();
        for (var index = 1; index <= 4; index++)
        {
            repository.Items.Add(CatalogItem.Create("SP" + index, "Hàng " + index, ProductCategory.SanitaryEquipment, "Toto", null, "Cái", null, null, null, null, 1m, 1m, 1m, 1m, 1m));
        }

        repository.Items.Add(CatalogItem.Create("NT01", "Tủ", ProductCategory.BathroomFurniture, "Toto", null, "Cái", null, null, null, null, 1m, 1m, 1m, 1m, 1m));
        repository.Items.Add(CatalogItem.CreateCombo("CB01", "Combo", ProductCategory.SanitaryEquipment, "Toto", null, "Bộ", null, null, null, null, 1m, 1m, 1m, 1m, 1m));
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var response = await client.GetAsync("/catalog?handler=Export&category=" + ProductCategoryToken.Sanitary);
        var text = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync()).TrimStart('\uFEFF');
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5, lines.Length);
        Assert.Contains("SP1", text, StringComparison.Ordinal);
        Assert.DoesNotContain("NT01", text, StringComparison.Ordinal);
        Assert.DoesNotContain("CB01", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(EmployeeRole.Sales, "sales")]
    [InlineData(EmployeeRole.ShowroomWarehouseLead, "lead")]
    [InlineData(EmployeeRole.WarehouseKeeper, "kho")]
    [InlineData(EmployeeRole.CustomerCare, "cskh")]
    public async Task OtherRoles_CannotOpenTheCatalogByLink(string role, string code)
    {
        using var client = _factory.CreateClient(NoRedirect);
        var repository = Reset();
        var item = CatalogItem.Create("SP01", "Vòi", ProductCategory.SanitaryEquipment, "Toto", null, "Cái", 12m, 0.4m, null, null, 8000000m, 15000000m, 12000000m, 11000000m, 10000000m);
        repository.Items.Add(item);
        await SignInAsync(client, role, code);

        var home = await client.GetAsync("/");
        var homeHtml = Decode(await home.Content.ReadAsStringAsync());
        var page = await client.GetAsync("/catalog?prices=" + item.Id.ToString("D"));
        var export = await client.GetAsync("/catalog?handler=Export");
        var homeToken = Regex.Match(homeHtml, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var posted = await client.PostAsync(
            "/catalog?handler=UpdatePrices",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Prices.Id"] = item.Id.ToString("D"),
                ["Prices.CostPrice"] = "1",
                ["Prices.RetailPrice"] = "1",
                ["Prices.DealerPrice"] = "1",
                ["Prices.DistributorPrice"] = "1",
                ["Prices.ProjectPrice"] = "1",
                ["__RequestVerificationToken"] = homeToken
            }));

        Assert.DoesNotContain("/catalog", homeHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Danh mục sản phẩm & giá", homeHtml, StringComparison.Ordinal);
        AssertDenied(page);
        AssertDenied(export);
        AssertDenied(posted);
        Assert.Equal(15000000m, item.RetailPrice);
    }

    [Fact]
    public async Task Accountant_SeesTheCatalogMenu_AndCanOpenThePage()
    {
        using var client = _factory.CreateClient(NoRedirect);
        Reset();
        await SignInAsync(client, EmployeeRole.Accountant, "accountant");

        var home = await client.GetAsync("/");
        var homeHtml = Decode(await home.Content.ReadAsStringAsync());
        var page = await client.GetAsync("/catalog");

        Assert.Contains("Danh mục sản phẩm & giá", homeHtml, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task Create_StoresDimensions_AndShowsTheUploadedImage()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var repository = Reset();
        await SignInAsync(client, EmployeeRole.Admin, "admin");
        var add = await client.GetAsync("/catalog?add=true");
        var token = Regex.Match(await add.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("SP77"), "Form.Code");
        content.Add(new StringContent("Vòi ảnh"), "Form.Name");
        content.Add(new StringContent(ProductCategoryToken.Sanitary), "Form.Category");
        content.Add(new StringContent("Cái"), "Form.Unit");
        content.Add(new StringContent("1000"), "Form.CostPrice");
        content.Add(new StringContent("2000"), "Form.RetailPrice");
        content.Add(new StringContent("1800"), "Form.DealerPrice");
        content.Add(new StringContent("1600"), "Form.DistributorPrice");
        content.Add(new StringContent("1500"), "Form.ProjectPrice");
        content.Add(new StringContent("800"), "Form.HeightMm");
        content.Add(new StringContent("1.200"), "Form.LengthMm");
        content.Add(new StringContent("450"), "Form.WidthMm");
        content.Add(new StringContent(token), "__RequestVerificationToken");
        var file = new ByteArrayContent(TinyPng);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(file, "Image", "voi.png");

        var posted = await client.PostAsync("/catalog?handler=Create", content);
        var item = Assert.Single(repository.Items);

        Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
        Assert.Equal(800, item.HeightMm);
        Assert.Equal(1200, item.LengthMm);
        Assert.Equal(450, item.WidthMm);
        Assert.StartsWith("/uploads/catalog/", item.ImagePath, StringComparison.Ordinal);
        var list = await client.GetAsync(posted.Headers.Location);
        var listHtml = await list.Content.ReadAsStringAsync();
        Assert.Contains(item.ImagePath, listHtml, StringComparison.Ordinal);
        _factory.Services.GetRequiredService<Pancras.Web.Catalog.CatalogImageStore>().Delete(item.ImagePath);
    }

    [Fact]
    public async Task Create_RejectsAPriceAboveTheColumn_OnTheForm()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var repository = Reset();
        await SignInAsync(client, EmployeeRole.Admin, "admin");
        var add = await client.GetAsync("/catalog?add=true");
        var token = Regex.Match(await add.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var posted = await client.PostAsync(
            "/catalog?handler=Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Form.Code"] = "SP88",
                ["Form.Name"] = "Giá quá lớn",
                ["Form.Category"] = ProductCategoryToken.Sanitary,
                ["Form.Unit"] = "Cái",
                ["Form.CostPrice"] = "1000",
                ["Form.RetailPrice"] = "100000000000000000000",
                ["Form.DealerPrice"] = "1000",
                ["Form.DistributorPrice"] = "1000",
                ["Form.ProjectPrice"] = "1000",
                ["__RequestVerificationToken"] = token
            }));
        var html = Decode(await posted.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        Assert.Contains("Giá lẻ vượt quá giới hạn cho phép.", html, StringComparison.Ordinal);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Create_RejectsABlankUnit()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var repository = Reset();
        await SignInAsync(client, EmployeeRole.Admin, "admin");
        var add = await client.GetAsync("/catalog?add=true");
        var token = Regex.Match(await add.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var posted = await client.PostAsync(
            "/catalog?handler=Create",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Form.Code"] = "SP89",
                ["Form.Name"] = "Thiếu đơn vị",
                ["Form.Category"] = ProductCategoryToken.Sanitary,
                ["Form.Unit"] = "",
                ["Form.CostPrice"] = "1000",
                ["Form.RetailPrice"] = "2000",
                ["Form.DealerPrice"] = "1800",
                ["Form.DistributorPrice"] = "1600",
                ["Form.ProjectPrice"] = "1500",
                ["__RequestVerificationToken"] = token
            }));
        var html = Decode(await posted.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        Assert.Contains("Đơn vị tính chưa được nhập.", html, StringComparison.Ordinal);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task StockedItem_LocksTheCostField()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var repository = Reset();
        var item = CatalogItem.Create("SP01", "Vòi", ProductCategory.SanitaryEquipment, "Toto", null, "Cái", null, null, null, null, 275000m, 300000m, 1m, 1m, 1m);
        repository.Items.Add(item);
        var stock = _factory.Services.GetRequiredService<InventoryDirectoryDouble>();
        stock.Balances.Clear();
        stock.Balances.Add(StockBalance.Create(item.Id, Guid.NewGuid(), 10, 0, 0));
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var prices = await client.GetAsync("/catalog?prices=" + item.Id.ToString("D"));
        var html = Decode(await prices.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, prices.StatusCode);
        Assert.Contains("disabled", html, StringComparison.Ordinal);
        Assert.Contains("Giá vốn không sửa được khi mặt hàng đã có tồn. Giá vốn cập nhật theo nhập kho.", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Prices.Version\"", html, StringComparison.Ordinal);
        stock.Balances.Clear();
    }

    private static readonly byte[] TinyPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89,
        0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54,
        0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00, 0x05, 0x00, 0x01,
        0x0D, 0x0A, 0x2D, 0xB4,
        0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    ];

    private static void AssertDenied(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/access-denied", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    private static string Decode(string html) => System.Net.WebUtility.HtmlDecode(html);

    private CatalogDirectoryDouble Reset()
    {
        var repository = _factory.Services.GetRequiredService<CatalogDirectoryDouble>();
        repository.Items.Clear();
        var employees = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        employees.Employees.Clear();
        return repository;
    }

    private async Task SignInAsync(HttpClient client, string role, string code)
    {
        var employees = _factory.Services.GetRequiredService<FakeEmployeeRepository>();
        var hasher = _factory.Services.GetRequiredService<FakePasswordHasher>();
        employees.Employees.RemoveAll(employee => employee.NormalizedCode == code.ToUpperInvariant());
        employees.Employees.Add(Employee.Create(code, code, code.ToUpperInvariant(), hasher.Hash("Admin@12345"), role));
        var login = await client.GetAsync("/login");
        var html = await login.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.EmployeeCode"] = code,
            ["Command.Password"] = "Admin@12345",
            ["__RequestVerificationToken"] = token
        });
        await client.PostAsync("/login", form);
    }

    private static WebApplicationFactoryClientOptions NoRedirect { get; } = new()
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    };
}

internal sealed class CatalogDirectoryDouble : ICatalogItemRepository
{
    public List<CatalogItem> Items { get; } = [];

    public Task<CatalogSlice> ListAsync(CatalogListFilter filter, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var ordered = Matching(filter);
        var size = pageSize < 1 ? CatalogPageRequest.DefaultSize : pageSize;
        var totalPages = ordered.Count == 0 ? 1 : (ordered.Count + size - 1) / size;
        var number = Math.Clamp(pageNumber < 1 ? 1 : pageNumber, 1, totalPages);
        IReadOnlyList<CatalogItem> page = ordered.Skip((number - 1) * size).Take(size).ToList();
        return Task.FromResult(new CatalogSlice(page, ordered.Count, number));
    }

    public Task<IReadOnlyList<CatalogItem>> ListMatchingAsync(CatalogListFilter filter, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CatalogItem>>(Matching(filter));

    public Task<CatalogItem?> GetVisibleReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(item => item.Id == id && !item.IsCombo));

    public Task<CatalogItem?> GetVisibleForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        GetVisibleReadOnlyAsync(id, cancellationToken);

    public Task<CatalogItem?> GetByIdReadOnlyAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

    public Task<bool> NormalizedCodeExistsAsync(string normalizedCode, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(item => item.NormalizedCode == normalizedCode));

    public Task<bool> TryAddAsync(CatalogItem item, CancellationToken cancellationToken)
    {
        Items.Add(item);
        return Task.FromResult(true);
    }

    public Task<Pancras.Application.Common.SaveOutcome> TrySaveAsync(CancellationToken cancellationToken) => Task.FromResult(Pancras.Application.Common.SaveOutcome.Saved);

    public Task<IReadOnlyDictionary<Guid, CatalogItemMeasure>> ListMeasuresAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, CatalogItemMeasure> rows = Items
            .Where(item => ids.Contains(item.Id))
            .ToDictionary(item => item.Id, item => new CatalogItemMeasure(item.Id, item.WeightKg, item.VolumeM3));
        return Task.FromResult(rows);
    }

    private List<CatalogItem> Matching(CatalogListFilter filter) =>
        Items.Where(filter.Matches).OrderBy(item => item.Name).ThenBy(item => item.Code).ToList();
}
