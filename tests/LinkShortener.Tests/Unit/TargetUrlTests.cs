using LinkShortener.Api.Application;

namespace LinkShortener.Tests.Unit;

public class TargetUrlTests
{
    [Theory]
    [InlineData("http://exemplo.com/pagina")]
    [InlineData("https://exemplo.com/pagina?x=1#topo")]
    [InlineData("https://8.8.8.8/dns")]
    public void Normalize_AcceptsPublicHttpAndHttps(string url)
    {
        var (normalized, error) = TargetUrl.Normalize(url);

        Assert.Null(error);
        Assert.Equal(url, normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://exemplo.com")]
    [InlineData("mailto:alguem@exemplo.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("exemplo.com")]
    [InlineData("https://")]
    [InlineData("nao e url")]
    public void Normalize_RejectsInvalidUrl(string? url)
    {
        var (normalized, error) = TargetUrl.Normalize(url);

        Assert.Null(normalized);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("http://localhost/admin")]
    [InlineData("http://app.localhost")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://127.10.0.1")]
    [InlineData("http://10.0.0.5")]
    [InlineData("http://172.16.0.1")]
    [InlineData("http://172.31.255.255")]
    [InlineData("http://192.168.0.10")]
    [InlineData("http://169.254.1.1")]
    [InlineData("http://0.0.0.0")]
    [InlineData("http://[::1]")]
    [InlineData("http://[fe80::1]")]
    [InlineData("http://[fd00::1]")]
    [InlineData("http://[::ffff:192.168.0.10]")]
    public void Normalize_RejectsLocalOrPrivateHost(string url)
    {
        var (_, error) = TargetUrl.Normalize(url);

        Assert.NotNull(error);
    }

    [Fact]
    public void Normalize_AcceptsPublicAddressNextToPrivateRange()
    {
        Assert.Null(TargetUrl.Normalize("http://172.32.0.1").Error);
    }

    [Fact]
    public void Normalize_AcceptsUpTo2048Characters()
    {
        var url = "https://exemplo.com/" + new string('a', 2048 - "https://exemplo.com/".Length);

        Assert.Null(TargetUrl.Normalize(url).Error);
        Assert.NotNull(TargetUrl.Normalize(url + "a").Error);
    }

    [Theory]
    [InlineData("HTTPS://Exemplo.com:443/", "https://exemplo.com")]
    [InlineData("  https://exemplo.com  ", "https://exemplo.com")]
    [InlineData("http://Exemplo.COM:80/", "http://exemplo.com")]
    [InlineData("http://exemplo.com:8080/", "http://exemplo.com:8080")]
    [InlineData("https://exemplo.com:80/a", "https://exemplo.com:80/a")]
    [InlineData("https://EXEMPLO.com/Caminho/?Q=Valor#Frag", "https://exemplo.com/Caminho/?Q=Valor#Frag")]
    [InlineData("https://exemplo.com/?q=1", "https://exemplo.com?q=1")]
    [InlineData("https://exemplo.com/a%20b", "https://exemplo.com/a%20b")]
    public void Normalize_NormalizesSchemeHostPortAndRootSlash(string url, string expected)
    {
        var (normalized, error) = TargetUrl.Normalize(url);

        Assert.Null(error);
        Assert.Equal(expected, normalized);
    }
}
