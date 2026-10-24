using LinkShortener.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace LinkShortener.Tests.Integration;

// Cada fábrica usa um arquivo SQLite próprio; DatabasePath permite simular reinício.
public class ApiFactory(string? databasePath = null) : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset StartTime = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    public string DatabasePath { get; } = databasePath ?? Path.Combine(Path.GetTempPath(), $"linkshortener-{Guid.NewGuid():N}.db");

    public FakeTimeProvider Time { get; } = new(StartTime);

    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:LinkShortener", $"Data Source={DatabasePath}");
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Time));
    }

    public void DeleteDatabase()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-shm", "-wal" })
            File.Delete(DatabasePath + suffix);
    }
}
