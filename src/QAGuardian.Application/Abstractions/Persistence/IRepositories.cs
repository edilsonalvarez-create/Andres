using System.Linq.Expressions;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Abstractions.Persistence;

/// <summary>Repositorio genérico (Repository Pattern).</summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    Task<(List<T> Items, int Total)> PagedAsync(int page, int pageSize,
        Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}

/// <summary>Unidad de trabajo: confirma los cambios de todos los repositorios en una transacción.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IProjectRepository : IRepository<Project>
{
    Task<Project?> GetWithModulesAsync(Guid id, CancellationToken ct = default);
    Task<Project?> GetByCodeAsync(string code, CancellationToken ct = default);
}

public interface ITestCaseRepository : IRepository<TestCase>
{
    Task<TestCase?> GetWithStepsAsync(Guid id, CancellationToken ct = default);
    Task<List<TestCase>> GetAutomatedByProjectAsync(Guid projectId, TestType? type, CancellationToken ct = default);
}

public interface ITestRunRepository : IRepository<TestRun>
{
    Task<TestRun?> GetWithResultsAsync(Guid id, CancellationToken ct = default);
    /// <summary>Carga TestRun con Results, Evidences, TestCases y Modules para matriz de ejecución.</summary>
    Task<TestRun?> GetWithFullDetailsAsync(Guid id, CancellationToken ct = default);
    /// <summary>Carga varias ejecuciones con sus resultados en una sola consulta (evita N+1 en dashboard).</summary>
    Task<List<TestRun>> ListWithResultsAsync(Expression<Func<TestRun, bool>> predicate, CancellationToken ct = default);
    Task<List<TestRun>> GetRecentByProjectAsync(Guid projectId, int count, CancellationToken ct = default);
    /// <summary>Página de ejecuciones cargando Results y GateEvaluation (necesarios para los conteos agregados del listado).</summary>
    Task<(List<TestRun> Items, int Total)> PagedWithDetailsAsync(int page, int pageSize, Guid projectId,
        CancellationToken ct = default);
}

public interface IDefectRepository : IRepository<Defect>
{
    Task<string> NextCodeAsync(Guid projectId, CancellationToken ct = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken ct = default);
    Task<List<Role>> GetRolesAsync(CancellationToken ct = default);
    /// <summary>Página de usuarios cargando sus roles (necesario para listarlos con su rol).</summary>
    Task<(List<User> Items, int Total)> PagedWithRolesAsync(int page, int pageSize, CancellationToken ct = default);
}

public interface IQualityGateRepository : IRepository<QualityGate>
{
    Task<QualityGate?> GetWithConditionsAsync(Guid id, CancellationToken ct = default);
    Task<QualityGate?> GetDefaultAsync(CancellationToken ct = default);
    /// <summary>Lista gates activos con condiciones en una sola consulta (evita N+1 en listados).</summary>
    Task<List<QualityGate>> ListWithConditionsAsync(CancellationToken ct = default);
}
