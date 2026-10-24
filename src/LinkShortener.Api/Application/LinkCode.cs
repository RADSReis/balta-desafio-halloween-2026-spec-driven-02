namespace LinkShortener.Api.Application;

public static class LinkCode
{
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    public const int GeneratedLength = 7;
    public const int CustomMinLength = 4;
    public const int CustomMaxLength = 20;

    // RF-03 e RF-09: trim e maiúsculas antes de validar ou buscar.
    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    // RN-07 e RN-08. Recebe o código já normalizado.
    public static string? ValidateCustom(string normalizedCode)
    {
        if (normalizedCode.Length is < CustomMinLength or > CustomMaxLength)
            return $"O código personalizado deve ter de {CustomMinLength} a {CustomMaxLength} caracteres.";

        if (!normalizedCode.All(c => Alphabet.Contains(c)))
            return "O código personalizado aceita somente letras de A a Z e dígitos de 0 a 9.";

        return null;
    }
}
