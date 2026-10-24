using LinkShortener.Api.Domain;

namespace LinkShortener.Api.Application;

public enum LinkResultStatus
{
    Ok,
    Created,
    Invalid,
    NotFound,
    Expired,
    Failed
}

public sealed record LinkResult<T>(LinkResultStatus Status, T? Value = default, string? Error = null)
{
    public static LinkResult<T> Ok(T value) => new(LinkResultStatus.Ok, value);
    public static LinkResult<T> Created(T value) => new(LinkResultStatus.Created, value);
    public static LinkResult<T> Invalid(string error) => new(LinkResultStatus.Invalid, Error: error);
    public static LinkResult<T> NotFound(string error) => new(LinkResultStatus.NotFound, Error: error);
    public static LinkResult<T> Expired(string error) => new(LinkResultStatus.Expired, Error: error);
    public static LinkResult<T> Failed(string error) => new(LinkResultStatus.Failed, Error: error);
}

public sealed record CreateLinkCommand(string? TargetUrl, string? Code, string? ExpiresAt);

public sealed record LinkStats(string Code, long AccessCount);

public sealed record LinkPage(IReadOnlyList<Link> Items, int Page, int PageSize, int TotalCount);
