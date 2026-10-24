using System.Globalization;

namespace LinkShortener.Api.Application;

public static class Expiration
{
    public const string DefaultTimeZoneId = "America/Sao_Paulo";

    private static readonly TimeZoneInfo DefaultTimeZone = TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);

    // RF-06 e RN-10. Devolve a expiração em UTC ou a mensagem de erro.
    public static (DateTime? ExpiresAtUtc, string? Error) Parse(string? rawValue, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return (null, "A data de expiração é obrigatória.");

        if (!DateTime.TryParse(rawValue.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            return (null, "A data de expiração não é uma data válida.");

        var expiresAtUtc = parsed.Kind switch
        {
            DateTimeKind.Utc => parsed,
            DateTimeKind.Local => parsed.ToUniversalTime(),
            _ => TimeZoneInfo.ConvertTimeToUtc(parsed, DefaultTimeZone)
        };

        if (expiresAtUtc.Date < nowUtc.Date)
            return (null, "A data de expiração não pode estar no passado.");

        if (expiresAtUtc.Date > nowUtc.AddYears(1).Date)
            return (null, "A data de expiração não pode ser posterior a 1 ano a partir de hoje.");

        return (expiresAtUtc, null);
    }
}
