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

/// <summary>Cifrado simétrico para secretos en reposo (tokens de integraciones / conn strings).</summary>
public interface ITokenEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

/// <summary>
/// Resuelve connection strings de entornos nombrados solo en Infrastructure (Sprint 12).
/// Nunca expone el secreto al cliente ni lo serializa en Hangfire.
/// </summary>
public interface IProjectDatabaseConnectionResolver
{
    /// <summary>Obtiene la connection string en claro para ejecutar validación de esquema.</summary>
    Task<string> ResolveAsync(Guid projectId, string environmentName, CancellationToken ct = default);

    /// <summary>True si el entorno existe (BD cifrada o configuración server-side).</summary>
    Task<bool> ExistsAsync(Guid projectId, string environmentName, CancellationToken ct = default);
}
