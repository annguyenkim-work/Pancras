using NetArchTest.Rules;

namespace Pancras.Infrastructure.Tests;

/// <summary>Khóa chiều phụ thuộc giữa các tầng (docs/02-coding-rules/layering-and-dependencies.md): Domain ← Application ← Infrastructure ← Web.</summary>
public class ArchitectureTests
{
    private static readonly System.Reflection.Assembly Domain = typeof(Pancras.Domain.Employees.Employee).Assembly;
    private static readonly System.Reflection.Assembly Application = typeof(Pancras.Application.Common.PageSizes).Assembly;

    [Fact]
    public void Domain_DoesNotDependOnOtherLayersOrEfCore()
    {
        var result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny("Pancras.Application", "Pancras.Infrastructure", "Pancras.Web", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_DoesNotDependOnInfrastructureWebOrEfCore()
    {
        var result = Types.InAssembly(Application)
            .ShouldNot()
            .HaveDependencyOnAny("Pancras.Infrastructure", "Pancras.Web", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnWeb()
    {
        var result = Types.InAssembly(typeof(Pancras.Infrastructure.Persistence.PancrasDbContext).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Pancras.Web")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.FailingTypeNames is null ? string.Empty : "Vi phạm: " + string.Join(", ", result.FailingTypeNames);
}
