using Pancras.Domain.Auditing;
using Pancras.Web.Formatting;

namespace Pancras.Web.Tests;

public class AuditLabelsTests
{
    [Fact]
    public void EveryAuditAction_HasAVietnameseLabel()
    {
        var codes = typeof(AuditActions).GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        Assert.NotEmpty(codes);
        var missing = codes.Where(code => AuditLabels.Of(code) == code).ToArray();
        Assert.Empty(missing);
    }

    [Fact]
    public void UnknownCodes_AreShownAsIs() =>
        Assert.Equal("legacy.thing", AuditLabels.Of("legacy.thing"));
}
