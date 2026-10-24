using LinkShortener.Api.Application;
using LinkShortener.Api.Domain;

namespace LinkShortener.Tests.Support;

public class FakeLinkRepository : ILinkRepository
{
    private readonly List<Link> _links = [];
    private int _nextId = 1;

    public int FindByCodeCalls { get; private set; }

    public IReadOnlyList<Link> Links => _links;

    public long AccessCountOf(string code) => _links.Single(l => l.Code == code).AccessCount;

    public void Seed(Link link)
    {
        SetProperty(link, nameof(Link.Id), _nextId++);
        _links.Add(link);
    }

    public Task<Link?> FindByCodeAsync(string code, CancellationToken cancellationToken)
    {
        FindByCodeCalls++;
        return Task.FromResult(_links.FirstOrDefault(l => l.Code == code));
    }

    public Task<Link?> FindByTargetUrlAsync(string targetUrl, CancellationToken cancellationToken) =>
        Task.FromResult(_links.FirstOrDefault(l => l.TargetUrl == targetUrl));

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(_links.Any(l => l.Code == code));

    public Task<(IReadOnlyList<Link> Items, int TotalCount)> ListAsync(int skip, int take, CancellationToken cancellationToken)
    {
        IReadOnlyList<Link> items = _links
            .OrderByDescending(l => l.CreatedAtUtc)
            .ThenByDescending(l => l.Id)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult((items, _links.Count));
    }

    public Task<bool> TryAddAsync(Link link, CancellationToken cancellationToken)
    {
        if (_links.Any(l => l.Code == link.Code))
            return Task.FromResult(false);

        Seed(link);
        return Task.FromResult(true);
    }

    public Task UpdateAsync(Link link, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task IncrementAccessCountAsync(int linkId, CancellationToken cancellationToken)
    {
        var link = _links.Single(l => l.Id == linkId);
        SetProperty(link, nameof(Link.AccessCount), link.AccessCount + 1);
        return Task.CompletedTask;
    }

    // Link só expõe setters privados; o EF faz o mesmo no repositório real.
    private static void SetProperty(Link link, string name, object value) =>
        typeof(Link).GetProperty(name)!.SetValue(link, value);
}

public class SequenceCodeGenerator(params string[] codes) : ICodeGenerator
{
    private readonly Queue<string> _codes = new(codes);

    public int Calls { get; private set; }

    public string Generate()
    {
        Calls++;
        return _codes.Dequeue();
    }
}
