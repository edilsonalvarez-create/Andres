using QAGuardian.Domain.Common;

namespace QAGuardian.Domain.Entities;

/// <summary>Usuario de la plataforma con roles RBAC.</summary>
public class User : AuditableEntity
{
    private readonly List<Role> _roles = [];
    private readonly List<RefreshToken> _refreshTokens = [];

    private User() { } // EF Core

    public User(string email, string fullName, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new DomainException("El correo electrónico no es válido.");
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("El nombre completo es obligatorio.");
        Email = email.Trim().ToLowerInvariant();
        FullName = fullName.Trim();
        PasswordHash = passwordHash;
        IsActive = true;
    }

    public string Email { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public int FailedLoginAttempts { get; private set; }
    public DateTime? LockedUntil { get; private set; }

    public IReadOnlyCollection<Role> Roles => _roles.AsReadOnly();
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    public bool IsLocked => LockedUntil.HasValue && LockedUntil > DateTime.UtcNow;

    public void AssignRole(Role role)
    {
        if (_roles.Any(r => r.Id == role.Id)) return;
        _roles.Add(role);
    }

    public void RemoveRole(Guid roleId) => _roles.RemoveAll(r => r.Id == roleId);

    public void RegisterSuccessfulLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        FailedLoginAttempts = 0;
        LockedUntil = null;
    }

    /// <summary>Bloqueo progresivo tras 5 intentos fallidos (protección fuerza bruta - OWASP).</summary>
    public void RegisterFailedLogin()
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= 5)
            LockedUntil = DateTime.UtcNow.AddMinutes(15);
    }

    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        foreach (var token in _refreshTokens.Where(t => t.IsActive))
            token.Revoke();
    }

    public RefreshToken AddRefreshToken(string token, DateTime expiresAt)
    {
        var refreshToken = new RefreshToken(Id, token, expiresAt);
        _refreshTokens.Add(refreshToken);
        return refreshToken;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
    public void UpdateProfile(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("El nombre completo es obligatorio.");
        FullName = fullName.Trim();
    }
}
