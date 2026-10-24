using LinkShortener.Api.Application;

namespace LinkShortener.Tests.Unit;

public class AccessClassifierTests
{
    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/130.0")]
    [InlineData("")]
    [InlineData(null)]
    public void IsCountable_CountsBrowserGet(string? userAgent)
    {
        Assert.True(AccessClassifier.IsCountable("GET", userAgent));
    }

    [Fact]
    public void IsCountable_IgnoresHead()
    {
        Assert.False(AccessClassifier.IsCountable("HEAD", "Mozilla/5.0"));
    }

    [Theory]
    [InlineData("Mozilla/5.0 (compatible; Googlebot/2.1)")]
    [InlineData("facebookexternalhit/1.1")]
    [InlineData("WhatsApp/2.23.20")]
    [InlineData("Slackbot-LinkExpanding 1.0")]
    [InlineData("TelegramBot (like TwitterBot)")]
    [InlineData("Mozilla/5.0 (compatible; Discordbot/2.0)")]
    public void IsCountable_IgnoresRobotsAndPreviewers(string userAgent)
    {
        Assert.False(AccessClassifier.IsCountable("GET", userAgent));
    }
}
