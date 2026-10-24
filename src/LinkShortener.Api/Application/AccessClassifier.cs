namespace LinkShortener.Api.Application;

public static class AccessClassifier
{
    private static readonly string[] NonHumanAgentMarkers =
    [
        "bot", "crawler", "spider", "slurp", "preview", "facebookexternalhit",
        "whatsapp", "embedly", "vkshare", "pinterest", "skypeuripreview", "outlook-ios-linkpreview"
    ];

    // RF-05 e RN-12: HEAD, robôs e pré-visualizadores não contam acesso.
    public static bool IsCountable(string method, string? userAgent)
    {
        if (HttpMethods.IsHead(method))
            return false;

        if (string.IsNullOrWhiteSpace(userAgent))
            return true;

        return !NonHumanAgentMarkers.Any(marker => userAgent.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
