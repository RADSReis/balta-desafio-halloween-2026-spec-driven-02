namespace LinkShortener.Api.Domain;

public class Link
{
    public int Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string TargetUrl { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public long AccessCount { get; private set; }

    private Link()
    {
    }

    public Link(string code, string targetUrl, DateTime createdAtUtc, DateTime expiresAtUtc)
    {
        Code = code;
        TargetUrl = targetUrl;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    // RN-11: válido até 23:59:59.999 UTC da data de expiração.
    public static bool IsExpired(DateTime expiresAtUtc, DateTime nowUtc) => nowUtc.Date > expiresAtUtc.Date;

    public bool IsExpiredAt(DateTime nowUtc) => IsExpired(ExpiresAtUtc, nowUtc);

    // RF-10: a nova expiração substitui a anterior, inclusive de link expirado.
    public void RenewExpiration(DateTime expiresAtUtc) => ExpiresAtUtc = expiresAtUtc;
}
