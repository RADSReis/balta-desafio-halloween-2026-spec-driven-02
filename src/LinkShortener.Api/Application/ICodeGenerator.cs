using System.Security.Cryptography;

namespace LinkShortener.Api.Application;

public interface ICodeGenerator
{
    string Generate();
}

// RF-02 e D-03: 7 caracteres de A-Z e 0-9, sorteados com gerador criptográfico.
public class RandomCodeGenerator : ICodeGenerator
{
    public string Generate() =>
        new(RandomNumberGenerator.GetItems<char>(LinkCode.Alphabet, LinkCode.GeneratedLength));
}
