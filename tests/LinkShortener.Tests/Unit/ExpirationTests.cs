using LinkShortener.Api.Application;

namespace LinkShortener.Tests.Unit;

public class ExpirationTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Parse_WithoutTimeZone_AssumesSaoPaulo()
    {
        var (expiresAtUtc, error) = Expiration.Parse("2026-10-24T22:00:00", Now);

        Assert.Null(error);
        Assert.Equal(new DateTime(2026, 10, 25, 1, 0, 0, DateTimeKind.Utc), expiresAtUtc);
        Assert.Equal(DateTimeKind.Utc, expiresAtUtc!.Value.Kind);
    }

    [Theory]
    [InlineData("2026-10-25T01:00:00Z")]
    [InlineData("2026-10-24T22:00:00-03:00")]
    [InlineData("2026-10-25T03:00:00+02:00")]
    public void Parse_WithTimeZone_ConvertsToUtc(string value)
    {
        var (expiresAtUtc, error) = Expiration.Parse(value, Now);

        Assert.Null(error);
        Assert.Equal(new DateTime(2026, 10, 25, 1, 0, 0, DateTimeKind.Utc), expiresAtUtc);
    }

    [Theory]
    [InlineData("2026-10-24T00:00:00Z")]
    [InlineData("2027-10-24T23:59:59Z")]
    public void Parse_AcceptsTodayAndOneYearAhead(string value)
    {
        Assert.Null(Expiration.Parse(value, Now).Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("amanhã")]
    [InlineData("2026-10-23T23:59:59Z")]
    [InlineData("2027-10-25T00:00:00Z")]
    public void Parse_RejectsMissingInvalidPastOrBeyondOneYear(string? value)
    {
        var (expiresAtUtc, error) = Expiration.Parse(value, Now);

        Assert.Null(expiresAtUtc);
        Assert.NotNull(error);
    }
}
