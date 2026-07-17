using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using QAGuardian.Infrastructure.Identity;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

/// <summary>Regresión Sprint 2 (OWASP A02:2025): el cifrado de tokens de integraciones en reposo
/// migró de AES-CBC sin autenticar a AES-GCM. Estas pruebas fijan el contrato: round-trip correcto
/// y detección de manipulación (lo que CBC sin HMAC no podía garantizar).</summary>
public class AesTokenEncryptionServiceTests
{
    private static AesTokenEncryptionService BuildService(string key = "clave-de-prueba-para-cifrado-32b")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:EncryptionKey"] = key })
            .Build();
        return new AesTokenEncryptionService(config);
    }

    [Fact]
    public void Encrypt_seguido_de_Decrypt_devuelve_el_texto_original()
    {
        var service = BuildService();
        const string secret = "ghp_TokenDeGitHubDeEjemplo1234567890";

        var cipherText = service.Encrypt(secret);
        var plainText = service.Decrypt(cipherText);

        plainText.Should().Be(secret);
    }

    [Fact]
    public void Dos_cifrados_del_mismo_texto_producen_salidas_distintas()
    {
        // Nonce aleatorio por operación (nunca se reutiliza): mismo plaintext, distinto ciphertext.
        var service = BuildService();
        const string secret = "mismo-secreto";

        var cipher1 = service.Encrypt(secret);
        var cipher2 = service.Encrypt(secret);

        cipher1.Should().NotBe(cipher2);
        service.Decrypt(cipher1).Should().Be(secret);
        service.Decrypt(cipher2).Should().Be(secret);
    }

    [Fact]
    public void Rechaza_un_ciphertext_manipulado_un_solo_bit()
    {
        // Esta es la propiedad que CBC-sin-HMAC NO garantizaba (bit-flipping silencioso).
        // GCM debe rechazar explícitamente cualquier alteración del ciphertext o del tag.
        var service = BuildService();
        var cipherBytes = Convert.FromBase64String(service.Encrypt("dato-sensible"));
        cipherBytes[^1] ^= 0x01; // altera el último byte (parte del ciphertext/tag)
        var tampered = Convert.ToBase64String(cipherBytes);

        var act = () => service.Decrypt(tampered);

        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    [Fact]
    public void Decrypt_de_un_texto_truncado_lanza_excepcion_controlada()
    {
        var service = BuildService();

        var act = () => service.Decrypt(Convert.ToBase64String(new byte[] { 1, 2, 3 }));

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Claves_distintas_no_pueden_descifrar_entre_si()
    {
        var serviceA = BuildService("clave-A-para-cifrado-de-32-bytes");
        var serviceB = BuildService("clave-B-completamente-diferente");

        var cipherText = serviceA.Encrypt("secreto");

        var act = () => serviceB.Decrypt(cipherText);

        act.Should().Throw<AuthenticationTagMismatchException>();
    }
}
