using QAGuardian.Domain.Entities;

namespace QAGuardian.Application.Abstractions.Services;

/// <summary>Usuario autenticado en el contexto de la petición actual.</summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsInRole(string role);
}

/// <summary>Emisión de tokens JWT y de refresco.</summary>
public interface IJwtTokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    int AccessTokenMinutes { get; }
    int RefreshTokenDays { get; }
}

/// <summary>Hash seguro de contraseñas (BCrypt).</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Cifrado simétrico para secretos en reposo (tokens de integraciones).</summary>
public interface ITokenEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
