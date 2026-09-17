using CarShell.Web.Validation;
using Xunit;

namespace CarShell.Web.Tests;

public class VinTests
{
    [Theory]
    [InlineData("1HGCM82633A004352")] // 17 chars, valid charset
    [InlineData("WVWZZZ1JZXW000001")]
    public void IsValid_accepts_well_formed_vins(string vin)
    {
        Assert.True(Vin.IsValid(vin));
    }

    [Theory]
    [InlineData("1HGCM82633A00435")] // 16 chars, too short
    [InlineData("1HGCM82633A0043522")] // 18 chars, too long
    [InlineData("1HGCM8263IA004352")] // contains I
    [InlineData("1HGCM8263OA004352")] // contains O
    [InlineData("1HGCM8263QA004352")] // contains Q
    [InlineData("")]
    public void IsValid_rejects_malformed_vins(string vin)
    {
        Assert.False(Vin.IsValid(vin));
    }

    [Fact]
    public void Normalize_trims_and_uppercases()
    {
        Assert.Equal("1HGCM82633A004352", Vin.Normalize(" 1hgcm82633a004352 "));
    }
}
