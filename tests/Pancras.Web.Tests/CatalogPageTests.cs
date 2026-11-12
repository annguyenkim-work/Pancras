using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pancras.Application.Catalog;
using Pancras.Application.Employees;
using Pancras.Domain.Catalog;
using Pancras.Domain.Employees;

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

        var response = await client.GetAsync("/danh-muc-gia");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/dang-nhap", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_ShowsVatNotice_FormatsMoney_AndKeepsMeasuresPlain()
    {
        using var client = _factory.CreateClient(NoRedirect);
        var repository = Reset();
        var item = CatalogItem.Create("SP01", "Vòi lavabo", ProductCategory.SanitaryEquipment, "Toto", "Việt Nam", "Cái", 12m, 0.4m, 6, 1m, 0m, 15000000m, 12000000m, 11000000m, 10000000m);
        repository.Items.Add(item);
        await SignInAsync(client, EmployeeRole.Admin, "admin");

        var page = await client.GetAsync("/danh-muc-gia");
        var html = await page.Content.ReadAsStringAsync();
        var detail = await client.GetAsync("/danh-muc-gia?xem=" + item.Id.ToString("D"));
        var detailHtml = await detail.Content.ReadAsStringAsync();
        var add = await client.GetAsync("/danh-muc-gia?them=true");
        var addHtml = await add.Content.ReadAsStringAsync();
        var prices = await client.GetAsync("/danh-muc-gia?gia=" + item.Id.ToString("D"));
        var priceHtml = await prices.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var text = Decode(html);
        var detailText = Decode(detailHtml);
        var addText = Decode(addHtml);
        var priceText = Decode(priceHtml);
        Assert.Contains("15.000.000 VNĐ", text, StringComparison.Ordinal);
        Assert.Contains("0 VNĐ", text, StringComparison.Ordinal);
        Assert.Contains("0,4", detailText, StringComparison.Ordinal);
        Assert.DoesNotContain("0,4 VNĐ", detailText, StringComparison.Ordinal);
        Assert.DoesNotContain("12 VNĐ", detailText, StringComparison.Ordinal);
        Assert.Contains("Giá đã gồm VAT.", addText, StringComparison.Ordinal);
        Assert.Contains("Giá đã gồm VAT.", priceText, StringComparison.Ordinal);
        Assert.Contains("Thêm sản phẩm vào danh mục", addText, StringComparison.Ordinal);
        Assert.Contains("Lưu sản phẩm", addText, StringComparison.Ordinal);
        Assert.Contains("Sửa sản phẩm", priceText, StringComparison.Ordinal);
        Assert.Contains("0 đ", addText, StringComparison.Ordinal);
        Assert.Contains("id=\"catalog-dialog\" class=\"fixed", addHtml, StringComparison.Ordinal);
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

        var response = await client.GetAsync("/danh-muc-gia?handler=Export&nganh=" + ProductCategoryToken.Sanitary);
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
        var page = await client.GetAsync("/danh-muc-gia?gia=" + item.Id.ToString("D"));
        var export = await client.GetAsync("/danh-muc-gia?handler=Export");
        var homeToken = Regex.Match(homeHtml, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var posted = await client.PostAsync(
            "/danh-muc-gia?handler=UpdatePrices",
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

        Assert.DoesNotContain("/danh-muc-gia", homeHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Danh mục sản phẩm và giá", homeHtml, StringComparison.Ordinal);
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
        var page = await client.GetAsync("/danh-muc-gia");

        Assert.Contains("Danh mục sản phẩm và giá", homeHtml, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    private static void AssertDenied(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/tu-choi", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
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
        var login = await client.GetAsync("/dang-nhap");
        var html = await login.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Command.EmployeeCode"] = code,
            ["Command.Password"] = "Admin@12345",
            ["__RequestVerificationToken"] = token
        });
        await client.PostAsync("/dang-nhap", form);
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

    public Task<bool> TrySaveAsync(CancellationToken cancellationToken) => Task.FromResult(true);

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
