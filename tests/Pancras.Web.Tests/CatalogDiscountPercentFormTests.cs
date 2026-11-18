using Pancras.Application.Catalog;
using Pancras.Application.Common;
using Pancras.Web.Formatting;

namespace Pancras.Web.Tests;

public class CatalogDiscountPercentFormTests
{
    [Theory]
    [InlineData("20", 20)]
    [InlineData("12,5", 12.5)]
    [InlineData("12.5", 12.5)]
    [InlineData("7,25", 7.25)]
    public void PriceForm_ReadsThePercentWithCommaOrDot(string text, double expected)
    {
        var form = ValidPriceForm();
        form.DealerDiscountPercent = text;

        var ok = CatalogFormReader.TryReadPrices(form, "Admin", out var command, out var issues);

        Assert.True(ok);
        Assert.Empty(issues);
        Assert.Equal((decimal)expected, command.DealerDiscountPercent);
        Assert.Null(command.DistributorDiscountPercent);
    }

    [Fact]
    public void PriceForm_TreatsEmptyPercentAsNotConfigured()
    {
        var ok = CatalogFormReader.TryReadPrices(ValidPriceForm(), "Admin", out var command, out _);

        Assert.True(ok);
        Assert.Null(command.DealerDiscountPercent);
        Assert.Null(command.DistributorDiscountPercent);
        Assert.Null(command.ProjectDiscountPercent);
    }

    [Fact]
    public void PriceForm_ReportsAPercentThatIsNotANumber()
    {
        var form = ValidPriceForm();
        form.ProjectDiscountPercent = "abc";

        var ok = CatalogFormReader.TryReadPrices(form, "Admin", out _, out var issues);

        Assert.False(ok);
        Assert.Contains(issues, issue => issue.Field == CatalogField.ProjectDiscountPercent && issue.Kind == CatalogIssueKind.InvalidNumber);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData(20.0, "20")]
    [InlineData(12.5, "12,5")]
    [InlineData(7.25, "7,25")]
    public void FormatPercent_DropsTrailingZerosAndUsesTheVietnameseComma(double? value, string expected) =>
        Assert.Equal(expected, VndMoney.FormatPercent(value is null ? null : (decimal)value.Value));

    private static CatalogPriceForm ValidPriceForm() => new()
    {
        Id = Guid.NewGuid(),
        CostPrice = "1000",
        RetailPrice = "2000",
        DealerPrice = "1800",
        DistributorPrice = "1700",
        ProjectPrice = "1600"
    };
}
