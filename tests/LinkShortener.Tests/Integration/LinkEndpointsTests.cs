using System.Net;
using System.Net.Http.Json;
using LinkShortener.Api.Application;
using LinkShortener.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LinkShortener.Tests.Integration;

public class LinkEndpointsTests : IDisposable
{
    private const string Tomorrow = "2026-10-25T12:00:00Z";

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public LinkEndpointsTests()
    {
        _client = _factory.CreateApiClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _factory.DeleteDatabase();
    }

    private sealed record CreateResponse(string ShortUrl, string Code, DateTime ExpiresAtUtc);

    private Task<HttpResponseMessage> PostLinkAsync(string targetUrl, string? code = null, string? expiresAt = Tomorrow) =>
        _client.PostAsJsonAsync("/api/links", new { targetUrl, code, expiresAt });

    private async Task<CreateResponse> CreateLinkAsync(string targetUrl, string? code = null)
    {
        var response = await PostLinkAsync(targetUrl, code);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateResponse>())!;
    }

    private async Task<long> GetAccessCountAsync(string code)
    {
        var stats = await _client.GetFromJsonAsync<LinkStats>($"/api/links/{code}/stats");
        return stats!.AccessCount;
    }

    [Fact]
    public async Task Migration_AppliesToDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Contains("InitialCreate", (await context.Database.GetAppliedMigrationsAsync()).Single());
    }

    [Fact]
    public async Task Create_WithoutCode_Returns201WithShortUrl()
    {
        var response = await PostLinkAsync("https://exemplo.com/artigo");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateResponse>();
        Assert.Matches("^[A-Z0-9]{7}$", body!.Code);
        Assert.Equal($"http://localhost/{body.Code}", body.ShortUrl);
        Assert.Equal(new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc), body.ExpiresAtUtc);
        Assert.Equal(body.ShortUrl, response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Create_ExpirationWithoutTimeZone_UsesSaoPaulo()
    {
        var response = await PostLinkAsync("https://exemplo.com", expiresAt: "2026-10-24T22:00:00");

        var body = await response.Content.ReadFromJsonAsync<CreateResponse>();
        Assert.Equal(new DateTime(2026, 10, 25, 1, 0, 0, DateTimeKind.Utc), body!.ExpiresAtUtc);
    }

    [Fact]
    public async Task Create_CustomCode_IsSavedUppercase()
    {
        var body = await CreateLinkAsync("https://exemplo.com", "  meulink ");

        Assert.Equal("MEULINK", body.Code);
    }

    [Fact]
    public async Task Create_DuplicateCustomCode_Returns400()
    {
        await CreateLinkAsync("https://exemplo.com/1", "MEULINK");

        var response = await PostLinkAsync("https://exemplo.com/2", "meulink");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("em uso", problem!.Detail);
    }

    [Theory]
    [InlineData("ftp://exemplo.com", null, Tomorrow)]
    [InlineData("http://localhost", null, Tomorrow)]
    [InlineData("http://127.0.0.1", null, Tomorrow)]
    [InlineData("http://192.168.0.10", null, Tomorrow)]
    [InlineData("https://exemplo.com", "  meu-link ", Tomorrow)]
    [InlineData("https://exemplo.com", "abc", Tomorrow)]
    [InlineData("https://exemplo.com", null, null)]
    [InlineData("https://exemplo.com", null, "2026-10-23T12:00:00Z")]
    [InlineData("https://exemplo.com", null, "2027-10-25T12:00:00Z")]
    public async Task Create_InvalidInput_Returns400ProblemDetails(string url, string? code, string? expiresAt)
    {
        var response = await PostLinkAsync(url, code, expiresAt);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Create_UrlLongerThan2048_Returns400()
    {
        var response = await PostLinkAsync("https://exemplo.com/" + new string('a', 2048));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_SameNormalizedUrl_ReturnsSameShortUrlAndKeepsCounter()
    {
        var first = await CreateLinkAsync("https://exemplo.com");
        await _client.GetAsync($"/{first.Code}");

        var response = await PostLinkAsync("HTTPS://Exemplo.com:443/", "OUTRO", "2026-12-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var second = await response.Content.ReadFromJsonAsync<CreateResponse>();
        Assert.Equal(first.ShortUrl, second!.ShortUrl);
        Assert.Equal(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), second.ExpiresAtUtc);
        Assert.Equal(1, await GetAccessCountAsync(first.Code));
    }

    [Fact]
    public async Task Redirect_ValidCode_Returns302ToTarget()
    {
        var link = await CreateLinkAsync("https://exemplo.com/destino?x=1");

        var response = await _client.GetAsync($"/{link.Code}");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://exemplo.com/destino?x=1", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Redirect_LowercaseCode_RedirectsToSameTarget()
    {
        await CreateLinkAsync("https://exemplo.com", "ABC1234");

        var response = await _client.GetAsync("/abc1234");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://exemplo.com", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Redirect_UnknownCode_Returns404()
    {
        var response = await _client.GetAsync("/NAOEXISTE");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Redirect_ExpiredLink_Returns410WithoutCounting()
    {
        var link = await CreateLinkAsync("https://exemplo.com");
        _factory.Time.UtcNow = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);

        var response = await _client.GetAsync($"/{link.Code}");

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal(0, await GetAccessCountAsync(link.Code));
    }

    [Fact]
    public async Task Redirect_HeadAndRobot_RedirectWithoutCounting()
    {
        var link = await CreateLinkAsync("https://exemplo.com");

        var head = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"/{link.Code}"));
        var robot = new HttpRequestMessage(HttpMethod.Get, $"/{link.Code}");
        robot.Headers.UserAgent.ParseAdd("facebookexternalhit/1.1");
        var robotResponse = await _client.SendAsync(robot);

        Assert.Equal(HttpStatusCode.Found, head.StatusCode);
        Assert.Equal(HttpStatusCode.Found, robotResponse.StatusCode);
        Assert.Equal(0, await GetAccessCountAsync(link.Code));
    }

    [Fact]
    public async Task Redirect_ConcurrentAccesses_DoNotLoseCount()
    {
        const int accesses = 50;
        var link = await CreateLinkAsync("https://exemplo.com");

        var responses = await Task.WhenAll(Enumerable.Range(0, accesses).Select(_ => _client.GetAsync($"/{link.Code}")));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Found, r.StatusCode));
        Assert.Equal(accesses, await GetAccessCountAsync(link.Code));
    }

    [Fact]
    public async Task Stats_ReturnsAccessCount()
    {
        var link = await CreateLinkAsync("https://exemplo.com");
        await _client.GetAsync($"/{link.Code}");
        await _client.GetAsync($"/{link.Code.ToLowerInvariant()}");

        var stats = await _client.GetFromJsonAsync<LinkStats>($"/api/links/{link.Code.ToLowerInvariant()}/stats");

        Assert.Equal(new LinkStats(link.Code, 2), stats);
    }

    [Fact]
    public async Task Stats_ExpiredLink_StillReturnsCount()
    {
        var link = await CreateLinkAsync("https://exemplo.com");
        await _client.GetAsync($"/{link.Code}");
        _factory.Time.UtcNow = ApiFactory.StartTime.AddDays(10);

        var response = await _client.GetAsync($"/api/links/{link.Code}/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await response.Content.ReadFromJsonAsync<LinkStats>())!.AccessCount);
    }

    [Fact]
    public async Task Stats_UnknownCode_Returns404()
    {
        var response = await _client.GetAsync("/api/links/NAOEXISTE/stats");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Restart_PersistedLinksKeepWorking()
    {
        var link = await CreateLinkAsync("https://exemplo.com/persistido");
        _client.Dispose();
        _factory.Dispose();

        using var restarted = new ApiFactory(_factory.DatabasePath);
        using var client = restarted.CreateApiClient();
        var response = await client.GetAsync($"/{link.Code}");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://exemplo.com/persistido", response.Headers.Location!.OriginalString);
    }

    private sealed record ListItem(string Code, string ShortUrl, string TargetUrl, DateTime ExpiresAtUtc, long AccessCount);

    private sealed record ListResponse(List<ListItem> Items, int Page, int PageSize, int TotalCount);

    [Fact]
    public async Task List_ReturnsAllLinksIncludingExpired()
    {
        var first = await CreateLinkAsync("https://exemplo.com/1", "PRIMEIRO");
        await _client.GetAsync($"/{first.Code}");
        _factory.Time.UtcNow = ApiFactory.StartTime.AddMinutes(1);
        await CreateLinkAsync("https://exemplo.com/2", "SEGUNDO");
        _factory.Time.UtcNow = ApiFactory.StartTime.AddDays(10);

        var response = await _client.GetFromJsonAsync<ListResponse>("/api/links");

        Assert.Equal(2, response!.TotalCount);
        Assert.Equal(["SEGUNDO", "PRIMEIRO"], response.Items.Select(i => i.Code));
        Assert.Equal(
            new ListItem("PRIMEIRO", "http://localhost/PRIMEIRO", "https://exemplo.com/1", new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc), 1),
            response.Items[1]);
    }

    [Fact]
    public async Task List_WithPaging_ReturnsRequestedPage()
    {
        await CreateLinkAsync("https://exemplo.com/1", "PRIMEIRO");
        _factory.Time.UtcNow = ApiFactory.StartTime.AddMinutes(1);
        await CreateLinkAsync("https://exemplo.com/2", "SEGUNDO");

        var response = await _client.GetFromJsonAsync<ListResponse>("/api/links?page=2&pageSize=1");

        Assert.Equal((2, 1, 2), (response!.Page, response.PageSize, response.TotalCount));
        Assert.Equal("PRIMEIRO", response.Items.Single().Code);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    public async Task List_InvalidPaging_Returns400ProblemDetails(string query)
    {
        var response = await _client.GetAsync($"/api/links{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }
}
