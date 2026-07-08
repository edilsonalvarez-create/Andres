using QAGuardian.Domain.Common;

namespace QAGuardian.Domain.Entities;

/// <summary>Token de refresco para renovación de sesiones JWT.</summary>
public class RefreshToken : BaseEntity
{
    private RefreshToken() { } // EF Core

    public RefreshToken(Guid userId, string token, DateTime expiresAt)
    {
        UserId = userId;
        Token = token;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid UserId { get; private set; }
    public string Token { get; private set; } = default!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => RevokedAt is null && !IsExpired;

    public void Revoke() => RevokedAt = DateTime.UtcNow;
}
