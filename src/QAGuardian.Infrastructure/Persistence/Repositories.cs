using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Infrastructure.Persistence;

/// <summary>Repositorio genérico basado en EF Core.</summary>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly QAGuardianDbContext Context;
    protected readonly DbSet<T> Set;

    public Repository(QAGuardianDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    public virtual Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<List<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
        => (predicate is null ? Set : Set.Where(predicate)).ToListAsync(ct);

    public async Task<(List<T> Items, int Total)> PagedAsync(int page, int pageSize,
        Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = predicate is null ? Set.AsQueryable() : Set.Where(predicate);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => Set.AnyAsync(predicate, ct);

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
        => predicate is null ? Set.CountAsync(ct) : Set.CountAsync(predicate, ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) => await Set.AddAsync(entity, ct);
    public void Update(T entity) => Set.Update(entity);
    public void Remove(T entity) => Set.Remove(entity);
}

public class ProjectRepository : Repository<Project>, IProjectRepository
{
    public ProjectRepository(QAGuardianDbContext context) : base(context) { }

    public Task<Project?> GetWithModulesAsync(Guid id, CancellationToken ct = default)
        => Set.Include(p => p.Modules).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Project?> GetByCodeAsync(string code, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(p => p.Code == code.ToUpper() && !p.IsDeleted, ct);
}

public class TestCaseRepository : Repository<TestCase>, ITestCaseRepository
{
    public TestCaseRepository(QAGuardianDbContext context) : base(context) { }

    public Task<TestCase?> GetWithStepsAsync(Guid id, CancellationToken ct = default)
        => Set.Include(t => t.Steps).FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<List<TestCase>> GetAutomatedByProjectAsync(Guid projectId, TestType? type, CancellationToken ct = default)
        => Set.Where(t => t.ProjectId == projectId && !t.IsDeleted
                && t.Framework != AutomationFramework.Manual
                && t.Status == TestCaseStatus.Active
                && (type == null || t.Type == type))
            .ToListAsync(ct);
}

public class TestRunRepository : Repository<TestRun>, ITestRunRepository
{
    public TestRunRepository(QAGuardianDbContext context) : base(context) { }

    public Task<TestRun?> GetWithResultsAsync(Guid id, CancellationToken ct = default)
        => Set.Include(r => r.Results).ThenInclude(res => res.Evidences)
              .Include(r => r.GateEvaluation)
              .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<List<TestRun>> GetRecentByProjectAsync(Guid projectId, int count, CancellationToken ct = default)
        => Set.Where(r => r.ProjectId == projectId && !r.IsDeleted)
              .OrderByDescending(r => r.CreatedAt).Take(count).ToListAsync(ct);
}

public class DefectRepository : Repository<Defect>, IDefectRepository
{
    public DefectRepository(QAGuardianDbContext context) : base(context) { }

    public async Task<string> NextCodeAsync(Guid projectId, CancellationToken ct = default)
    {
        var count = await Set.CountAsync(d => d.ProjectId == projectId, ct);
        return $"BUG-{count + 1:D5}";
    }
}

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(QAGuardianDbContext context) : base(context) { }

    public override Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Set.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => Set.Include(u => u.Roles).Include(u => u.RefreshTokens)
              .FirstOrDefaultAsync(u => u.Email == email.ToLower().Trim(), ct);

    public Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
        => Set.Include(u => u.Roles).Include(u => u.RefreshTokens)
              .FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == refreshToken), ct);

    public Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken ct = default)
        => Context.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);

    public Task<List<Role>> GetRolesAsync(CancellationToken ct = default)
        => Context.Roles.ToListAsync(ct);
}

public class QualityGateRepository : Repository<QualityGate>, IQualityGateRepository
{
    public QualityGateRepository(QAGuardianDbContext context) : base(context) { }

    public Task<QualityGate?> GetWithConditionsAsync(Guid id, CancellationToken ct = default)
        => Set.Include(g => g.Conditions).FirstOrDefaultAsync(g => g.Id == id, ct);

    public Task<QualityGate?> GetDefaultAsync(CancellationToken ct = default)
        => Set.Include(g => g.Conditions).FirstOrDefaultAsync(g => g.IsDefault && !g.IsDeleted, ct);
}

/// <summary>Unidad de trabajo: delega en el DbContext compartido por los repositorios.</summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly QAGuardianDbContext _context;

    public UnitOfWork(QAGuardianDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
