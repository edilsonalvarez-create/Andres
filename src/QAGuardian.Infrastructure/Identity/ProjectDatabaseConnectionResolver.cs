using Microsoft.Extensions.Configuration;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Infrastructure.Identity;

/// <summary>
/// Resuelve connection strings solo en servidor: BD cifrada o IConfiguration
/// (<c>ProjectDatabaseEnvironments:{projectId}:{name}</c>).
/// </summary>
public sealed class ProjectDatabaseConnectionResolver : IProjectDatabaseConnectionResolver
{
    private readonly IProjectDatabaseEnvironmentRepository _envs;
    private readonly ITokenEncryptionService _encryption;
    private readonly IConfiguration _configuration;

    public ProjectDatabaseConnectionResolver(
        IProjectDatabaseEnvironmentRepository envs,
        ITokenEncryptionService encryption,
        IConfiguration configuration)
    {
        _envs = envs;
        _encryption = encryption;
        _configuration = configuration;
    }

    public async Task<string> ResolveAsync(Guid projectId, string environmentName, CancellationToken ct = default)
    {
        var name = ProjectDatabaseEnvironment.NormalizeName(environmentName);
        var entity = await _envs.GetByNameAsync(projectId, name, ct);
        if (entity is not null && entity.IsActive && !entity.IsDeleted)
            return _encryption.Decrypt(entity.EncryptedConnectionString);

        var fromConfig = _configuration[$"ProjectDatabaseEnvironments:{projectId}:{name}"];
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return fromConfig.Trim();

        throw new NotFoundException(
            $"Entorno de BD '{name}' no configurado para el proyecto {projectId}.");
    }

    public async Task<bool> ExistsAsync(Guid projectId, string environmentName, CancellationToken ct = default)
    {
        var name = ProjectDatabaseEnvironment.NormalizeName(environmentName);
        var entity = await _envs.GetByNameAsync(projectId, name, ct);
        if (entity is not null && entity.IsActive && !entity.IsDeleted)
            return true;

        var fromConfig = _configuration[$"ProjectDatabaseEnvironments:{projectId}:{name}"];
        return !string.IsNullOrWhiteSpace(fromConfig);
    }
}
