using QAGuardian.Domain.Common;

namespace QAGuardian.Domain.Entities;

/// <summary>
/// Entorno de base de datos nombrado por proyecto (Sprint 12 / Threat Model secrets).
/// La connection string se almacena cifrada; la API de validación solo acepta el Name.
/// </summary>
public class ProjectDatabaseEnvironment : AuditableEntity
{
    private ProjectDatabaseEnvironment() { } // EF Core

    public ProjectDatabaseEnvironment(Guid projectId, string name, string encryptedConnectionString)
    {
        if (projectId == Guid.Empty)
            throw new DomainException("El proyecto del entorno de BD es obligatorio.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del entorno de BD es obligatorio.");
        if (string.IsNullOrWhiteSpace(encryptedConnectionString))
            throw new DomainException("La connection string cifrada es obligatoria.");

        ProjectId = projectId;
        Name = NormalizeName(name);
        EncryptedConnectionString = encryptedConnectionString;
        IsActive = true;
    }

    public Guid ProjectId { get; private set; }
    /// <summary>Nombre lógico (ej. "dev", "staging"). Comparación case-insensitive.</summary>
    public string Name { get; private set; } = default!;
    public string EncryptedConnectionString { get; private set; } = default!;
    public bool IsActive { get; private set; }

    public void UpdateEncryptedConnectionString(string encryptedConnectionString)
    {
        if (string.IsNullOrWhiteSpace(encryptedConnectionString))
            throw new DomainException("La connection string cifrada es obligatoria.");
        EncryptedConnectionString = encryptedConnectionString;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    public static string NormalizeName(string name) => name.Trim().ToLowerInvariant();
}
