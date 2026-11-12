using Pancras.Domain.Employees;

namespace Pancras.Domain.Tests;

public class EmployeeCodeGeneratorTests
{
    [Fact]
    public void NguyenKimAn_BecomesAnNK()
    {
        var code = EmployeeCodeGenerator.Generate("Nguyễn Kim An", _ => false);

        Assert.Equal("AnNK", code);
    }

    [Fact]
    public void VanThiThuThuy_BecomesThuyVTT()
    {
        var code = EmployeeCodeGenerator.Generate("Văn Thị Thu Thủy", _ => false);

        Assert.Equal("ThuyVTT", code);
    }

    [Fact]
    public void VoThiThuThuy_UsesSuffixWhenBaseIsTaken()
    {
        var code = EmployeeCodeGenerator.Generate(
            "Võ Thị Thu Thúy",
            normalized => normalized == "THUYVTT");

        Assert.Equal("ThuyVTT1", code);
    }

    [Fact]
    public void SingleWord_UsesTheWordWithFirstLetterUpper()
    {
        var code = EmployeeCodeGenerator.Generate("lan", _ => false);

        Assert.Equal("Lan", code);
    }

    [Fact]
    public void ReservedAdminCode_IsNotAssignedFromAName()
    {
        var code = EmployeeCodeGenerator.Generate("Admin", _ => false);

        Assert.NotEqual("ADMIN", code.ToUpperInvariant());
        Assert.Equal("Admin1", code);
    }

    [Fact]
    public void ExtraSpaces_DoNotChangeTheCode()
    {
        var code = EmployeeCodeGenerator.Generate("Nguyễn   Kim  An", _ => false);

        Assert.Equal("AnNK", code);
    }

    [Fact]
    public void NameWithoutLetters_YieldsNoCode()
    {
        Assert.Empty(EmployeeCodeGenerator.Attempts("---"));
    }
}
