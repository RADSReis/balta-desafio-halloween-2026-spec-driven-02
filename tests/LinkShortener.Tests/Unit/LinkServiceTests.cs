using LinkShortener.Api.Application;
using LinkShortener.Api.Domain;
using LinkShortener.Tests.Support;
using Microsoft.Extensions.Caching.Memory;

namespace LinkShortener.Tests.Unit;

public class LinkServiceTests
{
    private const string Browser = "Mozilla/5.0";
    private const string Tomorrow = "2026-10-25T12:00:00Z";

    private static readonly DateTime Now = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeLinkRepository _repository = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 100 });

    private LinkService CreateService(ICodeGenerator? generator = null) =>
        new(_repository, generator ?? new RandomCodeGenerator(), _cache, _time);

    [Fact]
    public async Task CreateAsync_WithoutCode_GeneratesCode()
    {
        var result = await CreateService().CreateAsync(new("https://exemplo.com/a", null, Tomorrow), default);

        Assert.Equal(LinkResultStatus.Created, result.Status);
        Assert.Equal(7, result.Value!.Code.Length);
        Assert.Equal("https://exemplo.com/a", result.Value.TargetUrl);
        Assert.Equal(Now, result.Value.CreatedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_GeneratedCodeInUse_TriesAnotherCode()
    {
        _repository.Seed(new Link("AAAAAAA", "https://outro.com", Now, Now));
        var generator = new SequenceCodeGenerator("AAAAAAA", "BBBBBBB");

        var result = await CreateService(generator).CreateAsync(new("https://exemplo.com", null, Tomorrow), default);

        Assert.Equal(LinkResultStatus.Created, result.Status);
        Assert.Equal("BBBBBBB", result.Value!.Code);
    }

    [Fact]
    public async Task CreateAsync_FiveCollisions_Fails()
    {
        _repository.Seed(new Link("AAAAAAA", "https://outro.com", Now, Now));
        var generator = new SequenceCodeGenerator(Enumerable.Repeat("AAAAAAA", 6).ToArray());

        var result = await CreateService(generator).CreateAsync(new("https://exemplo.com", null, Tomorrow), default);

        Assert.Equal(LinkResultStatus.Failed, result.Status);
        Assert.Equal(LinkService.MaxGenerationAttempts, generator.Calls);
    }

    [Fact]
    public async Task CreateAsync_CustomCode_IsNormalized()
    {
        var result = await CreateService().CreateAsync(new("https://exemplo.com", "  meulink ", Tomorrow), default);

        Assert.Equal(LinkResultStatus.Created, result.Status);
        Assert.Equal("MEULINK", result.Value!.Code);
    }

    [Theory]
    [InlineData("  meu-link ")]
    [InlineData("abc")]
    [InlineData("abcdefghij01234567890")]
    public async Task CreateAsync_InvalidCustomCode_IsInvalid(string code)
    {
        var result = await CreateService().CreateAsync(new("https://exemplo.com", code, Tomorrow), default);

        Assert.Equal(LinkResultStatus.Invalid, result.Status);
        Assert.Empty(_repository.Links);
    }

    [Fact]
    public async Task CreateAsync_CustomCodeInUse_IsInvalid()
    {
        var service = CreateService();
        await service.CreateAsync(new("https://exemplo.com/1", "MEULINK", Tomorrow), default);

        var result = await service.CreateAsync(new("https://exemplo.com/2", "meulink", Tomorrow), default);

        Assert.Equal(LinkResultStatus.Invalid, result.Status);
        Assert.Contains("em uso", result.Error);
    }

    [Theory]
    [InlineData("ftp://exemplo.com", Tomorrow)]
    [InlineData("http://localhost", Tomorrow)]
    [InlineData("https://exemplo.com", null)]
    [InlineData("https://exemplo.com", "2026-10-23T12:00:00Z")]
    public async Task CreateAsync_InvalidUrlOrExpiration_IsInvalid(string url, string? expiresAt)
    {
        var result = await CreateService().CreateAsync(new(url, null, expiresAt), default);

        Assert.Equal(LinkResultStatus.Invalid, result.Status);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task CreateAsync_SameNormalizedUrl_ReusesLinkAndReplacesExpiration()
    {
        var service = CreateService();
        var first = await service.CreateAsync(new("https://exemplo.com", null, Tomorrow), default);
        await service.ResolveAsync(first.Value!.Code, "GET", Browser, default);

        var second = await service.CreateAsync(new("HTTPS://Exemplo.com:443/", "OUTROCODIGO", "2026-12-01T00:00:00Z"), default);

        Assert.Equal(LinkResultStatus.Ok, second.Status);
        Assert.Equal(first.Value.Code, second.Value!.Code);
        Assert.Equal(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), second.Value.ExpiresAtUtc);
        Assert.Single(_repository.Links);
        Assert.Equal(1, _repository.AccessCountOf(first.Value.Code));
    }

    [Fact]
    public async Task CreateAsync_SameUrlOfExpiredLink_ReactivatesLink()
    {
        var service = CreateService();
        var first = await service.CreateAsync(new("https://exemplo.com", null, Tomorrow), default);
        _time.UtcNow = Now.AddDays(5);
        Assert.Equal(LinkResultStatus.Expired, (await service.ResolveAsync(first.Value!.Code, "GET", Browser, default)).Status);

        await service.CreateAsync(new("https://exemplo.com", null, "2026-11-10T00:00:00Z"), default);

        var resolved = await service.ResolveAsync(first.Value.Code, "GET", Browser, default);
        Assert.Equal(LinkResultStatus.Ok, resolved.Status);
    }

    [Fact]
    public async Task ResolveAsync_LowercaseCode_FindsLinkAndCountsAccess()
    {
        var service = CreateService();
        await service.CreateAsync(new("https://exemplo.com", "ABC1234", Tomorrow), default);

        var result = await service.ResolveAsync("abc1234", "GET", Browser, default);
        await service.ResolveAsync("ABC1234", "GET", Browser, default);

        Assert.Equal("https://exemplo.com", result.Value);
        Assert.Equal(2, _repository.AccessCountOf("ABC1234"));
    }

    [Fact]
    public async Task ResolveAsync_UnknownCode_IsNotFound()
    {
        var result = await CreateService().ResolveAsync("NAOEXISTE", "GET", Browser, default);

        Assert.Equal(LinkResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ResolveAsync_ExpiredLink_IsExpiredAndDoesNotCount()
    {
        var service = CreateService();
        await service.CreateAsync(new("https://exemplo.com", "ABC1234", Tomorrow), default);

        _time.UtcNow = new DateTime(2026, 10, 25, 23, 59, 59, 999, DateTimeKind.Utc);
        Assert.Equal(LinkResultStatus.Ok, (await service.ResolveAsync("ABC1234", "GET", Browser, default)).Status);

        _time.UtcNow = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);
        var result = await service.ResolveAsync("ABC1234", "GET", Browser, default);

        Assert.Equal(LinkResultStatus.Expired, result.Status);
        Assert.Equal(1, _repository.AccessCountOf("ABC1234"));
    }

    [Fact]
    public async Task ResolveAsync_HeadOrRobot_RedirectsWithoutCounting()
    {
        var service = CreateService();
        await service.CreateAsync(new("https://exemplo.com", "ABC1234", Tomorrow), default);

        var head = await service.ResolveAsync("ABC1234", "HEAD", Browser, default);
        var robot = await service.ResolveAsync("ABC1234", "GET", "Googlebot/2.1", default);

        Assert.Equal(LinkResultStatus.Ok, head.Status);
        Assert.Equal(LinkResultStatus.Ok, robot.Status);
        Assert.Equal(0, _repository.AccessCountOf("ABC1234"));
    }

    [Fact]
    public async Task ResolveAsync_SecondAccess_UsesCacheInsteadOfRepository()
    {
        var service = CreateService();
        await service.CreateAsync(new("https://exemplo.com", "ABC1234", Tomorrow), default);

        await service.ResolveAsync("ABC1234", "GET", Browser, default);
        await service.ResolveAsync("abc1234", "GET", Browser, default);

        Assert.Equal(1, _repository.FindByCodeCalls);
        Assert.Equal(2, _repository.AccessCountOf("ABC1234"));
    }

    [Fact]
    public async Task ResolveAsync_AfterExpirationRenewal_DoesNotUseStaleCache()
    {
        var service = CreateService();
        await service.CreateAsync(new("https://exemplo.com", "ABC1234", Tomorrow), default);
        await service.ResolveAsync("ABC1234", "GET", Browser, default);

        await service.CreateAsync(new("https://exemplo.com", null, "2026-11-10T00:00:00Z"), default);
        _time.UtcNow = Now.AddDays(5);

        var result = await service.ResolveAsync("ABC1234", "GET", Browser, default);

        Assert.Equal(LinkResultStatus.Ok, result.Status);
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsCountEvenWhenExpired()
    {
        var service = CreateService();
        await service.CreateAsync(new("https://exemplo.com", "ABC1234", Tomorrow), default);
        await service.ResolveAsync("ABC1234", "GET", Browser, default);
        _time.UtcNow = Now.AddDays(5);

        var result = await service.GetStatsAsync("abc1234", default);

        Assert.Equal(new LinkStats("ABC1234", 1), result.Value);
        Assert.Equal(LinkResultStatus.Ok, result.Status);
    }

    [Fact]
    public async Task GetStatsAsync_UnknownCode_IsNotFound()
    {
        var result = await CreateService().GetStatsAsync("NAOEXISTE", default);

        Assert.Equal(LinkResultStatus.NotFound, result.Status);
    }

}
