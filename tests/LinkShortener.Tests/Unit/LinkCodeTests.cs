using LinkShortener.Api.Application;

namespace LinkShortener.Tests.Unit;

public class LinkCodeTests
{
    [Fact]
    public void Generate_ReturnsSevenCharactersFromAlphabet()
    {
        var generator = new RandomCodeGenerator();

        for (var i = 0; i < 1000; i++)
        {
            var code = generator.Generate();

            Assert.Equal(7, code.Length);
            Assert.All(code, c => Assert.Contains(c, LinkCode.Alphabet));
        }
    }

    [Fact]
    public void Alphabet_IsUppercaseLettersAndDigits()
    {
        Assert.Equal("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", LinkCode.Alphabet);
    }

    [Fact]
    public void Normalize_TrimsAndUppercases()
    {
        Assert.Equal("MEULINK", LinkCode.Normalize("  meulink "));
    }

    [Theory]
    [InlineData("MEULINK")]
    [InlineData("ABCD")]
    [InlineData("ABCDEFGHIJ0123456789")]
    public void ValidateCustom_AcceptsValidCode(string code)
    {
        Assert.Null(LinkCode.ValidateCustom(code));
    }

    [Theory]
    [InlineData("MEU-LINK")]
    [InlineData("MEU LINK")]
    [InlineData("LINKÇ")]
    [InlineData("ABC")]
    [InlineData("ABCDEFGHIJ01234567890")]
    public void ValidateCustom_RejectsInvalidCode(string code)
    {
        Assert.NotNull(LinkCode.ValidateCustom(code));
    }
}
