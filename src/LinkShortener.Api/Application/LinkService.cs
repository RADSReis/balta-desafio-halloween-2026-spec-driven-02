using LinkShortener.Api.Domain;
using Microsoft.Extensions.Caching.Memory;

namespace LinkShortener.Api.Application;

public class LinkService(
    ILinkRepository repository,
    ICodeGenerator codeGenerator,
    IMemoryCache cache,
    TimeProvider timeProvider)
{
    public const int MaxGenerationAttempts = 5;

    private const string NotFoundMessage = "Link não encontrado.";

    private static readonly MemoryCacheEntryOptions CacheEntryOptions = new()
    {
        Size = 1,
        SlidingExpiration = TimeSpan.FromMinutes(10)
    };

    public async Task<LinkResult<Link>> CreateAsync(CreateLinkCommand command, CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        var (targetUrl, urlError) = TargetUrl.Normalize(command.TargetUrl);
        if (urlError is not null)
            return LinkResult<Link>.Invalid(urlError);

        var (expiresAtUtc, expirationError) = Expiration.Parse(command.ExpiresAt, nowUtc);
        if (expirationError is not null)
            return LinkResult<Link>.Invalid(expirationError);

        // RF-10: mesma URL normalizada reaproveita o link e ignora o código informado.
        var existing = await repository.FindByTargetUrlAsync(targetUrl!, cancellationToken);
        if (existing is not null)
        {
            existing.RenewExpiration(expiresAtUtc!.Value);
            await repository.UpdateAsync(existing, cancellationToken);
            cache.Remove(CacheKey(existing.Code));
            return LinkResult<Link>.Ok(existing);
        }

        return string.IsNullOrWhiteSpace(command.Code)
            ? await CreateWithGeneratedCodeAsync(targetUrl!, nowUtc, expiresAtUtc!.Value, cancellationToken)
            : await CreateWithCustomCodeAsync(command.Code, targetUrl!, nowUtc, expiresAtUtc!.Value, cancellationToken);
    }

    public async Task<LinkResult<string>> ResolveAsync(string code, string method, string? userAgent, CancellationToken cancellationToken)
    {
        var normalizedCode = LinkCode.Normalize(code);

        var link = await FindCachedAsync(normalizedCode, cancellationToken);
        if (link is null)
            return LinkResult<string>.NotFound(NotFoundMessage);

        // RN-05: link expirado não redireciona e não conta acesso.
        if (Link.IsExpired(link.ExpiresAtUtc, timeProvider.GetUtcNow().UtcDateTime))
            return LinkResult<string>.Expired("Este link expirou.");

        if (AccessClassifier.IsCountable(method, userAgent))
            await repository.IncrementAccessCountAsync(link.Id, cancellationToken);

        return LinkResult<string>.Ok(link.TargetUrl);
    }

    // RF-07: qualquer pessoa que conheça o código, inclusive de link expirado.
    public async Task<LinkResult<LinkStats>> GetStatsAsync(string code, CancellationToken cancellationToken)
    {
        var link = await repository.FindByCodeAsync(LinkCode.Normalize(code), cancellationToken);

        return link is null
            ? LinkResult<LinkStats>.NotFound(NotFoundMessage)
            : LinkResult<LinkStats>.Ok(new LinkStats(link.Code, link.AccessCount));
    }

    private async Task<LinkResult<Link>> CreateWithCustomCodeAsync(
        string rawCode, string targetUrl, DateTime nowUtc, DateTime expiresAtUtc, CancellationToken cancellationToken)
    {
        var code = LinkCode.Normalize(rawCode);

        var codeError = LinkCode.ValidateCustom(code);
        if (codeError is not null)
            return LinkResult<Link>.Invalid(codeError);

        const string codeInUseMessage = "O código informado já está em uso por outro link.";

        // RN-01: a checagem antecipa o erro; o índice único (D-04) cobre a corrida.
        if (await repository.CodeExistsAsync(code, cancellationToken))
            return LinkResult<Link>.Invalid(codeInUseMessage);

        var link = new Link(code, targetUrl, nowUtc, expiresAtUtc);
        return await repository.TryAddAsync(link, cancellationToken)
            ? LinkResult<Link>.Created(link)
            : LinkResult<Link>.Invalid(codeInUseMessage);
    }

    // RN-09: até 5 tentativas quando o código gerado já está em uso.
    private async Task<LinkResult<Link>> CreateWithGeneratedCodeAsync(
        string targetUrl, DateTime nowUtc, DateTime expiresAtUtc, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            var code = codeGenerator.Generate();

            if (await repository.CodeExistsAsync(code, cancellationToken))
                continue;

            var link = new Link(code, targetUrl, nowUtc, expiresAtUtc);
            if (await repository.TryAddAsync(link, cancellationToken))
                return LinkResult<Link>.Created(link);
        }

        return LinkResult<Link>.Failed("Não foi possível gerar um código único para o link. Tente novamente.");
    }

    // D-08: links acessados ficam em cache; o contador continua no banco.
    private async Task<CachedLink?> FindCachedAsync(string code, CancellationToken cancellationToken)
    {
        var key = CacheKey(code);
        if (cache.TryGetValue(key, out CachedLink? cached))
            return cached;

        var link = await repository.FindByCodeAsync(code, cancellationToken);
        if (link is null)
            return null;

        cached = new CachedLink(link.Id, link.TargetUrl, link.ExpiresAtUtc);
        cache.Set(key, cached, CacheEntryOptions);
        return cached;
    }

    private static string CacheKey(string code) => $"link:{code}";

    private sealed record CachedLink(int Id, string TargetUrl, DateTime ExpiresAtUtc);
}
