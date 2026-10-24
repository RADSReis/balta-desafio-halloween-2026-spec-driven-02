using LinkShortener.Api.Application;
using LinkShortener.Api.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LinkShortener.Api.Data;

public class LinkRepository(AppDbContext context) : ILinkRepository
{
    private const int SqliteConstraintError = 19;

    public Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
        context.Links.FirstOrDefaultAsync(l => l.Code == code, cancellationToken);

    public Task<Link?> FindByTargetUrlAsync(string targetUrl, CancellationToken cancellationToken) =>
        context.Links.FirstOrDefaultAsync(l => l.TargetUrl == targetUrl, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
        context.Links.AnyAsync(l => l.Code == code, cancellationToken);

    public async Task<(IReadOnlyList<Link> Items, int TotalCount)> ListAsync(int skip, int take, CancellationToken cancellationToken)
    {
        var totalCount = await context.Links.CountAsync(cancellationToken);
        var items = await context.Links
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAtUtc)
            .ThenByDescending(l => l.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken)
    {
        context.Links.Add(link);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintError })
        {
            context.Entry(link).State = EntityState.Detached;
            return false;
        }
    }

    public Task UpdateAsync(Link link, CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);

    public Task IncrementAccessCountAsync(int linkId, CancellationToken cancellationToken) =>
        context.Links
            .Where(l => l.Id == linkId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(l => l.AccessCount, l => l.AccessCount + 1), cancellationToken);
}
