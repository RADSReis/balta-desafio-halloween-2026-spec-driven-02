using LinkShortener.Api.Domain;

namespace LinkShortener.Tests.Unit;

public class LinkTests
{
    private static readonly DateTime ExpiresAt = new(2026, 10, 25, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsExpiredAt_ValidUntilEndOfUtcDay()
    {
        var link = new Link("ABC1234", "https://exemplo.com", ExpiresAt.AddDays(-1), ExpiresAt);

        Assert.False(link.IsExpiredAt(new DateTime(2026, 10, 25, 23, 59, 59, 999, DateTimeKind.Utc)));
        Assert.True(link.IsExpiredAt(new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void RenewExpiration_ReactivatesExpiredLink()
    {
        var link = new Link("ABC1234", "https://exemplo.com", ExpiresAt.AddDays(-1), ExpiresAt);
        var now = ExpiresAt.AddDays(3);

        link.RenewExpiration(now.AddDays(10));

        Assert.False(link.IsExpiredAt(now));
    }
}
