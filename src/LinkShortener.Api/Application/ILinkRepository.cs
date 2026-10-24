using LinkShortener.Api.Domain;

namespace LinkShortener.Api.Application;

public interface ILinkRepository
{
    Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken);
    Task<Link?> FindByTargetUrlAsync(string targetUrl, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);

    // Devolve false quando o índice único do código recusa a inserção (D-04).
    Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken);
    Task UpdateAsync(Link link, CancellationToken cancellationToken);

    // D-02: incremento atômico feito pelo banco.
    Task IncrementAccessCountAsync(int linkId, CancellationToken cancellationToken);
}
