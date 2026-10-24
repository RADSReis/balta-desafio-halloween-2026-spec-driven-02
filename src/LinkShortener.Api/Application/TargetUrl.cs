using System.Net;
using System.Net.Sockets;

namespace LinkShortener.Api.Application;

public static class TargetUrl
{
    public const int MaxLength = 2048;

    // RN-02, RN-03, RN-04 e RN-13. Devolve a URL normalizada ou a mensagem de erro.
    public static (string? NormalizedUrl, string? Error) Normalize(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return (null, "A URL de destino é obrigatória.");

        var trimmed = rawUrl.Trim();

        if (trimmed.Length > MaxLength)
            return (null, $"A URL de destino deve ter no máximo {MaxLength} caracteres.");

        var schemeSeparator = trimmed.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparator <= 0 || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return (null, "A URL de destino não é uma URL válida.");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return (null, "A URL de destino precisa usar http ou https.");

        if (string.IsNullOrEmpty(uri.Host))
            return (null, "A URL de destino não é uma URL válida.");

        if (!IsPublicHost(uri))
            return (null, "A URL de destino precisa apontar para um endereço público.");

        // Caminho, query e fragmento são mantidos como vieram; só a autoridade é reescrita.
        var authorityStart = schemeSeparator + 3;
        var authorityEnd = trimmed.IndexOfAny(['/', '?', '#'], authorityStart);
        var rest = authorityEnd < 0 ? string.Empty : trimmed[authorityEnd..];

        if (rest == "/" || rest.StartsWith("/?", StringComparison.Ordinal) || rest.StartsWith("/#", StringComparison.Ordinal))
            rest = rest[1..];

        var userInfo = string.IsNullOrEmpty(uri.UserInfo) ? string.Empty : uri.UserInfo + "@";
        var port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port;
        var normalized = $"{uri.Scheme}://{userInfo}{uri.Host.ToLowerInvariant()}{port}{rest}";

        return (normalized, null);
    }

    private static bool IsPublicHost(Uri uri)
    {
        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();

        if (host == "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal))
            return false;

        if (uri.HostNameType is not (UriHostNameType.IPv4 or UriHostNameType.IPv6))
            return true;

        if (!IPAddress.TryParse(host.Trim('[', ']'), out var address))
            return false;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return false;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal);

        var bytes = address.GetAddressBytes();
        return bytes switch
        {
            [10, ..] => false,
            [172, >= 16 and <= 31, ..] => false,
            [192, 168, ..] => false,
            [169, 254, ..] => false,
            _ => true
        };
    }
}
