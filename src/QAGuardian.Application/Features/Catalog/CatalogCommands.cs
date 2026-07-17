using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Application.Features.Catalog;

// ─────────────────────────── DTOs ───────────────────────────

public record RequirementDto(Guid Id, Guid ModuleId, string Code, string Title, string? Description);
public record UserStoryDto(Guid Id, Guid RequirementId, string Title, string? AcceptanceCriteria);
public record ProjectVersionDto(Guid Id, Guid ProjectId, string Number, string? Notes, DateTime? ReleasedAt);

// ─────────────────────────── Requerimientos ───────────────────────────

public record CreateRequirementCommand(Guid ModuleId, string Code, string Title, string? Description)
    : IRequest<Result<RequirementDto>>;

public class CreateRequirementCommandValidator : AbstractValidator<CreateRequirementCommand>
{
    public CreateRequirementCommandValidator()
    {
        RuleFor(x => x.ModuleId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
    }
}

public class CreateRequirementCommandHandler : IRequestHandler<CreateRequirementCommand, Result<RequirementDto>>
{
    private readonly IRepository<Module> _modules;
    private readonly IRepository<Requirement> _requirements;
    private readonly IProjectAccessService _access;
    private readonly IUnitOfWork _uow;

    public CreateRequirementCommandHandler(
        IRepository<Module> modules, IRepository<Requirement> requirements,
        IProjectAccessService access, IUnitOfWork uow)
    {
        _modules = modules;
        _requirements = requirements;
        _access = access;
        _uow = uow;
    }

    public async Task<Result<RequirementDto>> Handle(CreateRequirementCommand request, CancellationToken ct)
    {
        var module = await _modules.GetByIdAsync(request.ModuleId, ct);
        if (module is null || !await _access.CanAccessProjectAsync(module.ProjectId, ct))
            throw new NotFoundException(nameof(Module), request.ModuleId);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await _requirements.AnyAsync(r => r.ModuleId == request.ModuleId && r.Code == code && !r.IsDeleted, ct))
            return Result<RequirementDto>.Failure($"Ya existe un requerimiento con el código '{code}' en el módulo.");

        var requirement = new Requirement(request.ModuleId, request.Code, request.Title, request.Description);
        await _requirements.AddAsync(requirement, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<RequirementDto>.Success(new RequirementDto(
            requirement.Id, requirement.ModuleId, requirement.Code, requirement.Title, requirement.Description));
    }
}

public record GetRequirementsByModuleQuery(Guid ModuleId) : IRequest<IReadOnlyList<RequirementDto>>;

public class GetRequirementsByModuleQueryHandler
    : IRequestHandler<GetRequirementsByModuleQuery, IReadOnlyList<RequirementDto>>
{
    private readonly IRepository<Module> _modules;
    private readonly IRepository<Requirement> _requirements;
    private readonly IProjectAccessService _access;

    public GetRequirementsByModuleQueryHandler(
        IRepository<Module> modules, IRepository<Requirement> requirements, IProjectAccessService access)
    {
        _modules = modules;
        _requirements = requirements;
        _access = access;
    }

    public async Task<IReadOnlyList<RequirementDto>> Handle(GetRequirementsByModuleQuery request, CancellationToken ct)
    {
        var module = await _modules.GetByIdAsync(request.ModuleId, ct);
        if (module is null || !await _access.CanAccessProjectAsync(module.ProjectId, ct))
            throw new NotFoundException(nameof(Module), request.ModuleId);

        var items = await _requirements.ListAsync(r => r.ModuleId == request.ModuleId && !r.IsDeleted, ct);
        return items.Select(r => new RequirementDto(r.Id, r.ModuleId, r.Code, r.Title, r.Description)).ToList();
    }
}

// ─────────────────────────── Historias de usuario ───────────────────────────

public record CreateUserStoryCommand(Guid RequirementId, string Title, string? AcceptanceCriteria)
    : IRequest<Result<UserStoryDto>>;

public class CreateUserStoryCommandValidator : AbstractValidator<CreateUserStoryCommand>
{
    public CreateUserStoryCommandValidator()
    {
        RuleFor(x => x.RequirementId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
    }
}

public class CreateUserStoryCommandHandler : IRequestHandler<CreateUserStoryCommand, Result<UserStoryDto>>
{
    private readonly IRepository<Requirement> _requirements;
    private readonly IRepository<Module> _modules;
    private readonly IRepository<UserStory> _stories;
    private readonly IProjectAccessService _access;
    private readonly IUnitOfWork _uow;

    public CreateUserStoryCommandHandler(
        IRepository<Requirement> requirements, IRepository<Module> modules,
        IRepository<UserStory> stories, IProjectAccessService access, IUnitOfWork uow)
    {
        _requirements = requirements;
        _modules = modules;
        _stories = stories;
        _access = access;
        _uow = uow;
    }

    public async Task<Result<UserStoryDto>> Handle(CreateUserStoryCommand request, CancellationToken ct)
    {
        await EnsureRequirementAccessAsync(request.RequirementId, ct);

        var story = new UserStory(request.RequirementId, request.Title, request.AcceptanceCriteria);
        await _stories.AddAsync(story, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<UserStoryDto>.Success(new UserStoryDto(
            story.Id, story.RequirementId, story.Title, story.AcceptanceCriteria));
    }

    private async Task EnsureRequirementAccessAsync(Guid requirementId, CancellationToken ct)
    {
        var requirement = await _requirements.GetByIdAsync(requirementId, ct);
        if (requirement is null)
            throw new NotFoundException(nameof(Requirement), requirementId);

        var module = await _modules.GetByIdAsync(requirement.ModuleId, ct);
        if (module is null || !await _access.CanAccessProjectAsync(module.ProjectId, ct))
            throw new NotFoundException(nameof(Requirement), requirementId);
    }
}

public record GetUserStoriesByRequirementQuery(Guid RequirementId) : IRequest<IReadOnlyList<UserStoryDto>>;

public class GetUserStoriesByRequirementQueryHandler
    : IRequestHandler<GetUserStoriesByRequirementQuery, IReadOnlyList<UserStoryDto>>
{
    private readonly IRepository<Requirement> _requirements;
    private readonly IRepository<Module> _modules;
    private readonly IRepository<UserStory> _stories;
    private readonly IProjectAccessService _access;

    public GetUserStoriesByRequirementQueryHandler(
        IRepository<Requirement> requirements, IRepository<Module> modules,
        IRepository<UserStory> stories, IProjectAccessService access)
    {
        _requirements = requirements;
        _modules = modules;
        _stories = stories;
        _access = access;
    }

    public async Task<IReadOnlyList<UserStoryDto>> Handle(GetUserStoriesByRequirementQuery request, CancellationToken ct)
    {
        var requirement = await _requirements.GetByIdAsync(request.RequirementId, ct);
        if (requirement is null)
            throw new NotFoundException(nameof(Requirement), request.RequirementId);

        var module = await _modules.GetByIdAsync(requirement.ModuleId, ct);
        if (module is null || !await _access.CanAccessProjectAsync(module.ProjectId, ct))
            throw new NotFoundException(nameof(Requirement), request.RequirementId);

        var items = await _stories.ListAsync(s => s.RequirementId == request.RequirementId && !s.IsDeleted, ct);
        return items.Select(s => new UserStoryDto(s.Id, s.RequirementId, s.Title, s.AcceptanceCriteria)).ToList();
    }
}

// ─────────────────────────── Versiones de proyecto ───────────────────────────

public record CreateProjectVersionCommand(Guid ProjectId, string Number, string? Notes)
    : IRequest<Result<ProjectVersionDto>>;

public class CreateProjectVersionCommandValidator : AbstractValidator<CreateProjectVersionCommand>
{
    public CreateProjectVersionCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Number).NotEmpty().MaximumLength(50);
    }
}

public class CreateProjectVersionCommandHandler
    : IRequestHandler<CreateProjectVersionCommand, Result<ProjectVersionDto>>
{
    private readonly IProjectRepository _projects;
    private readonly IRepository<ProjectVersion> _versions;
    private readonly IProjectAccessService _access;
    private readonly IUnitOfWork _uow;

    public CreateProjectVersionCommandHandler(
        IProjectRepository projects, IRepository<ProjectVersion> versions,
        IProjectAccessService access, IUnitOfWork uow)
    {
        _projects = projects;
        _versions = versions;
        _access = access;
        _uow = uow;
    }

    public async Task<Result<ProjectVersionDto>> Handle(CreateProjectVersionCommand request, CancellationToken ct)
    {
        await _access.EnsureCanAccessProjectAsync(request.ProjectId, ct);
        _ = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), request.ProjectId);

        var number = request.Number.Trim();
        if (await _versions.AnyAsync(v => v.ProjectId == request.ProjectId && v.Number == number, ct))
            return Result<ProjectVersionDto>.Failure($"La versión '{number}' ya existe en el proyecto.");

        var version = new ProjectVersion(request.ProjectId, request.Number, request.Notes);
        await _versions.AddAsync(version, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<ProjectVersionDto>.Success(new ProjectVersionDto(
            version.Id, version.ProjectId, version.Number, version.Notes, version.ReleasedAt));
    }
}

public record ReleaseProjectVersionCommand(Guid VersionId) : IRequest<Result<ProjectVersionDto>>;

public class ReleaseProjectVersionCommandHandler
    : IRequestHandler<ReleaseProjectVersionCommand, Result<ProjectVersionDto>>
{
    private readonly IRepository<ProjectVersion> _versions;
    private readonly IProjectAccessService _access;
    private readonly IUnitOfWork _uow;

    public ReleaseProjectVersionCommandHandler(
        IRepository<ProjectVersion> versions, IProjectAccessService access, IUnitOfWork uow)
    {
        _versions = versions;
        _access = access;
        _uow = uow;
    }

    public async Task<Result<ProjectVersionDto>> Handle(ReleaseProjectVersionCommand request, CancellationToken ct)
    {
        var version = await _versions.GetByIdAsync(request.VersionId, ct);
        if (version is null || !await _access.CanAccessProjectAsync(version.ProjectId, ct))
            throw new NotFoundException(nameof(ProjectVersion), request.VersionId);
        version.MarkReleased();
        await _uow.SaveChangesAsync(ct);
        return Result<ProjectVersionDto>.Success(new ProjectVersionDto(
            version.Id, version.ProjectId, version.Number, version.Notes, version.ReleasedAt));
    }
}

public record GetProjectVersionsQuery(Guid ProjectId) : IRequest<IReadOnlyList<ProjectVersionDto>>;

public class GetProjectVersionsQueryHandler
    : IRequestHandler<GetProjectVersionsQuery, IReadOnlyList<ProjectVersionDto>>
{
    private readonly IRepository<ProjectVersion> _versions;
    private readonly IProjectAccessService _access;

    public GetProjectVersionsQueryHandler(IRepository<ProjectVersion> versions, IProjectAccessService access)
    {
        _versions = versions;
        _access = access;
    }

    public async Task<IReadOnlyList<ProjectVersionDto>> Handle(GetProjectVersionsQuery request, CancellationToken ct)
    {
        await _access.EnsureCanAccessProjectAsync(request.ProjectId, ct);
        var items = await _versions.ListAsync(v => v.ProjectId == request.ProjectId && !v.IsDeleted, ct);
        return items.OrderByDescending(v => v.CreatedAt)
            .Select(v => new ProjectVersionDto(v.Id, v.ProjectId, v.Number, v.Notes, v.ReleasedAt)).ToList();
    }
}

// ─────────────────────────── Módulos (listar para el catálogo) ───────────────────────────

public record ModuleListDto(Guid Id, string Name, string? Description);

public record GetModulesByProjectQuery(Guid ProjectId) : IRequest<IReadOnlyList<ModuleListDto>>;

public class GetModulesByProjectQueryHandler
    : IRequestHandler<GetModulesByProjectQuery, IReadOnlyList<ModuleListDto>>
{
    private readonly IRepository<Module> _modules;
    private readonly IProjectAccessService _access;

    public GetModulesByProjectQueryHandler(IRepository<Module> modules, IProjectAccessService access)
    {
        _modules = modules;
        _access = access;
    }

    public async Task<IReadOnlyList<ModuleListDto>> Handle(GetModulesByProjectQuery request, CancellationToken ct)
    {
        await _access.EnsureCanAccessProjectAsync(request.ProjectId, ct);
        var items = await _modules.ListAsync(m => m.ProjectId == request.ProjectId && !m.IsDeleted, ct);
        return items.Select(m => new ModuleListDto(m.Id, m.Name, m.Description)).ToList();
    }
}
