using LinkShortener.Api.Application;

namespace LinkShortener.Api.Endpoints;

public static class LinkEndpoints
{
    public sealed record CreateLinkRequest(string? TargetUrl, string? Code, string? ExpiresAt);

    public sealed record CreateLinkResponse(string ShortUrl, string Code, DateTime ExpiresAtUtc);

    public sealed record LinkItemResponse(string Code, string ShortUrl, string TargetUrl, DateTime ExpiresAtUtc, long AccessCount);

    public sealed record LinkListResponse(IReadOnlyList<LinkItemResponse> Items, int Page, int PageSize, int TotalCount);

    public static IEndpointRouteBuilder MapLinkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/links", CreateAsync);
        app.MapGet("/api/links", ListAsync);
        app.MapGet("/api/links/{code}/stats", GetStatsAsync);
        app.MapMethods("/{code}", [HttpMethods.Get, HttpMethods.Head], RedirectAsync);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateLinkRequest request, LinkService service, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(
            new CreateLinkCommand(request.TargetUrl, request.Code, request.ExpiresAt), cancellationToken);

        if (result.Value is not { } link)
            return Problem(result);

        var shortUrl = ShortUrl(context, link.Code);
        var response = new CreateLinkResponse(shortUrl, link.Code, link.ExpiresAtUtc);

        return result.Status == LinkResultStatus.Created
            ? Results.Created(shortUrl, response)
            : Results.Ok(response);
    }

    private static async Task<IResult> RedirectAsync(
        string code, LinkService service, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.ResolveAsync(
            code, context.Request.Method, context.Request.Headers.UserAgent.ToString(), cancellationToken);

        // RF-04: sempre 302, nunca 301.
        return result.Value is { } targetUrl
            ? Results.Redirect(targetUrl, permanent: false)
            : Problem(result);
    }

    private static async Task<IResult> ListAsync(
        int? page, int? pageSize, LinkService service, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(page, pageSize, cancellationToken);

        if (result.Value is not { } linkPage)
            return Problem(result);

        var items = linkPage.Items
            .Select(l => new LinkItemResponse(l.Code, ShortUrl(context, l.Code), l.TargetUrl, l.ExpiresAtUtc, l.AccessCount))
            .ToList();

        return Results.Ok(new LinkListResponse(items, linkPage.Page, linkPage.PageSize, linkPage.TotalCount));
    }

    private static async Task<IResult> GetStatsAsync(string code, LinkService service, CancellationToken cancellationToken)
    {
        var result = await service.GetStatsAsync(code, cancellationToken);

        return result.Value is { } stats ? Results.Ok(stats) : Problem(result);
    }

    // RF-01: URL curta montada com o esquema e o host da própria requisição.
    private static string ShortUrl(HttpContext context, string code) =>
        $"{context.Request.Scheme}://{context.Request.Host}/{code}";

    private static IResult Problem<T>(LinkResult<T> result)
    {
        var (statusCode, title) = result.Status switch
        {
            LinkResultStatus.Invalid => (StatusCodes.Status400BadRequest, "Requisição inválida"),
            LinkResultStatus.NotFound => (StatusCodes.Status404NotFound, "Não encontrado"),
            LinkResultStatus.Expired => (StatusCodes.Status410Gone, "Link expirado"),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno")
        };

        return Results.Problem(detail: result.Error, statusCode: statusCode, title: title);
    }
}
