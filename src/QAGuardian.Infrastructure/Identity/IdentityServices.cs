using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Infrastructure.Identity;

/// <summary>Opciones de JWT leídas de configuración.</summary>
public class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "QAGuardian";
    public string Audience { get; set; } = "QAGuardian.Clients";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 7;
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    /// <summary>Mínimo de 256 bits (32 bytes UTF-8) requerido por HS256 (RFC 7518 §3.2; OWASP A02:2025).</summary>
    private const int MinSigningKeyBytes = 32;

    public JwtTokenService(IConfiguration configuration)
    {
        _options = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(_options.SigningKey))
            throw new InvalidOperationException(
                "Jwt:SigningKey no está configurada. Defina una clave de al menos 32 caracteres.");
        if (Encoding.UTF8.GetByteCount(_options.SigningKey) < MinSigningKeyBytes)
            throw new InvalidOperationException(
                $"Jwt:SigningKey es demasiado corta ({Encoding.UTF8.GetByteCount(_options.SigningKey)} bytes). " +
                $"HS256 requiere una clave de al menos {MinSigningKeyBytes} bytes (256 bits) para resistir " +
                "ataques de fuerza bruta contra la firma.");
    }

    public int AccessTokenMinutes => _options.AccessTokenMinutes;
    public int RefreshTokenDays => _options.RefreshTokenDays;

    public string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r.Name)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}

/// <summary>Hash de contraseñas con BCrypt (factor de trabajo 12).</summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}

/// <summary>
/// Cifrado autenticado AES-256-GCM para secretos de integraciones en reposo (OWASP A02:2025).
/// Reemplaza un esquema previo de AES-CBC sin autenticación (vulnerable a padding oracle y
/// bit-flipping): GCM aporta confidencialidad e integridad en una sola operación. El formato de
/// salida (nonce‖tag‖ciphertext) es incompatible con el anterior: tras desplegar esta versión,
/// los tokens de integraciones ya guardados deben volver a configurarse una vez (ver runbook).
/// </summary>
public class AesTokenEncryptionService : ITokenEncryptionService
{
    private const int NonceSizeBytes = 12; // 96 bits, tamaño recomendado por NIST SP 800-38D
    private const int TagSizeBytes = 16;   // 128 bits
    /// <summary>Material mínimo antes de derivar con SHA-256 (OWASP A02:2025 / B7).</summary>
    public const int MinEncryptionKeyChars = 32;

    private readonly byte[] _key;

    public AesTokenEncryptionService(IConfiguration configuration)
    {
        var keyMaterial = configuration["Security:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(keyMaterial))
            throw new InvalidOperationException(
                "Security:EncryptionKey no está configurada. Defina una clave de al menos " +
                $"{MinEncryptionKeyChars} caracteres (user-secrets, variable de entorno " +
                "Security__EncryptionKey o secret manager). Una clave vacía produciría material " +
                "determinista (SHA256(\"\")) e inseguro.");
        if (keyMaterial.Length < MinEncryptionKeyChars)
            throw new InvalidOperationException(
                $"Security:EncryptionKey es demasiado corta ({keyMaterial.Length} caracteres). " +
                $"Se requieren al menos {MinEncryptionKeyChars} caracteres de material aleatorio.");
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial));
    }

    public string Encrypt(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var gcm = new AesGcm(_key, TagSizeBytes);
        gcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var result = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, nonce.Length + tag.Length, cipherBytes.Length);
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        var data = Convert.FromBase64String(cipherText);
        if (data.Length < NonceSizeBytes + TagSizeBytes)
            throw new CryptographicException("Texto cifrado inválido o truncado.");

        var nonce = data.AsSpan(0, NonceSizeBytes);
        var tag = data.AsSpan(NonceSizeBytes, TagSizeBytes);
        var cipherBytes = data.AsSpan(NonceSizeBytes + TagSizeBytes);
        var plainBytes = new byte[cipherBytes.Length];

        using var gcm = new AesGcm(_key, TagSizeBytes);
        gcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
