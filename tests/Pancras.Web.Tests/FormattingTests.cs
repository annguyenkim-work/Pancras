using Pancras.Web.Formatting;

namespace Pancras.Web.Tests;

public class FormattingTests
{
    [Theory]
    [InlineData(1234.5, "1.234,5 USD")]
    [InlineData(10, "10 USD")]
    [InlineData(0.125, "0,13 USD")]
    public void FormatUsd_UsesVietnameseSeparators_AndAtMostTwoDecimals(double amount, string expected) =>
        Assert.Equal(expected, VndMoney.FormatUsd((decimal)amount));

    [Theory]
    [InlineData(0.1, "10%")]
    [InlineData(0.0825, "8,25%")]
    [InlineData(0, "0%")]
    public void FormatPercent_TakesAFraction(double fraction, string expected) =>
        Assert.Equal(expected, VndMoney.FormatPercent((decimal)fraction));

    [Fact]
    public void VietnamTime_IsFixedAtUtcPlus7_RegardlessOfTheServerZone()
    {
        var utc = new DateTimeOffset(2026, 11, 16, 20, 30, 0, TimeSpan.Zero);

        Assert.Equal("17/11/2026 03:30", VietnamTime.Format(utc));
        Assert.Equal("2026-11-17 03:30", VietnamTime.Format(utc, "yyyy-MM-dd HH:mm"));
        Assert.Equal(new DateOnly(2026, 11, 17), VietnamTime.DateOf(utc));
    }
}
