using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;

namespace QAGuardian.Application.Features.Projects;

public record ProjectDto(
    Guid Id, string Code, string Name, string? Description, string? RepositoryUrl,
    bool IsActive, Guid? QualityGateId, int ModulesCount, DateTime CreatedAt);

public record ModuleDto(Guid Id, string Name, string? Description);

public static class ProjectMapper
{
    public static ProjectDto ToDto(this Project p) => new(
        p.Id, p.Code, p.Name, p.Description, p.RepositoryUrl,
        p.IsActive, p.QualityGateId, p.Modules.Count(m => !m.IsDeleted), p.CreatedAt);
}

// ─────────────────────────── Crear proyecto ───────────────────────────

public record CreateProjectCommand(string Code, string Name, string? Description, string? RepositoryUrl)
    : IRequest<Result<ProjectDto>>;

public class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9-]+$")
            .WithMessage("El código solo admite letras, números y guiones.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RepositoryUrl).Must(url => string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("La URL del repositorio no es válida.");
    }
}

public class CreateProjectCommandHandler : IRequestHandler<CreateProjectCommand, Result<ProjectDto>>
{
    private readonly IProjectRepository _projects;
    private readonly IQualityGateRepository _gates;
    private readonly IUnitOfWork _uow;

    public CreateProjectCommandHandler(IProjectRepository projects, IQualityGateRepository gates, IUnitOfWork uow)
    {
        _projects = projects;
        _gates = gates;
        _uow = uow;
    }

    public async Task<Result<ProjectDto>> Handle(CreateProjectCommand request, CancellationToken ct)
    {
        var existing = await _projects.GetByCodeAsync(request.Code, ct);
        if (existing is not null)
            return Result<ProjectDto>.Failure($"Ya existe un proyecto con el código '{request.Code}'.");

        var project = new Project(request.Code, request.Name, request.Description, request.RepositoryUrl);

        var defaultGate = await _gates.GetDefaultAsync(ct);
        if (defaultGate is not null)
            project.AssignQualityGate(defaultGate.Id);

        await _projects.AddAsync(project, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<ProjectDto>.Success(project.ToDto());
    }
}

// ─────────────────────────── Actualizar proyecto ───────────────────────────

public record UpdateProjectCommand(Guid Id, string Name, string? Description, string? RepositoryUrl)
    : IRequest<Result<ProjectDto>>;

public class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand, Result<ProjectDto>>
{
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _uow;

    public UpdateProjectCommandHandler(IProjectRepository projects, IUnitOfWork uow)
    {
        _projects = projects;
        _uow = uow;
    }

    public async Task<Result<ProjectDto>> Handle(UpdateProjectCommand request, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(Project), request.Id);
        project.Update(request.Name, request.Description, request.RepositoryUrl);
        await _uow.SaveChangesAsync(ct);
        return Result<ProjectDto>.Success(project.ToDto());
    }
}

// ─────────────────────────── Eliminar (soft delete) ───────────────────────────

public record DeleteProjectCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand, Result<bool>>
{
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _uow;

    public DeleteProjectCommandHandler(IProjectRepository projects, IUnitOfWork uow)
    {
        _projects = projects;
        _uow = uow;
    }

    public async Task<Result<bool>> Handle(DeleteProjectCommand request, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(Project), request.Id);
        project.IsDeleted = true;
        project.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ─────────────────────────── Agregar módulo ───────────────────────────

public record AddModuleCommand(Guid ProjectId, string Name, string? Description) : IRequest<Result<ModuleDto>>;

public class AddModuleCommandValidator : AbstractValidator<AddModuleCommand>
{
    public AddModuleCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public class AddModuleCommandHandler : IRequestHandler<AddModuleCommand, Result<ModuleDto>>
{
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _uow;

    public AddModuleCommandHandler(IProjectRepository projects, IUnitOfWork uow)
    {
        _projects = projects;
        _uow = uow;
    }

    public async Task<Result<ModuleDto>> Handle(AddModuleCommand request, CancellationToken ct)
    {
        var project = await _projects.GetWithModulesAsync(request.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), request.ProjectId);
        var module = project.AddModule(request.Name, request.Description);
        await _uow.SaveChangesAsync(ct);
        return Result<ModuleDto>.Success(new ModuleDto(module.Id, module.Name, module.Description));
    }
}

// ─────────────────────────── Consultas ───────────────────────────

public record GetProjectsQuery(int Page = 1, int PageSize = 20, string? Search = null)
    : IRequest<PagedResult<ProjectDto>>;

public class GetProjectsQueryHandler : IRequestHandler<GetProjectsQuery, PagedResult<ProjectDto>>
{
    private readonly IProjectRepository _projects;

    public GetProjectsQueryHandler(IProjectRepository projects) => _projects = projects;

    public async Task<PagedResult<ProjectDto>> Handle(GetProjectsQuery request, CancellationToken ct)
    {
        var search = request.Search?.Trim();
        var (items, total) = await _projects.PagedAsync(
            request.Page, request.PageSize,
            p => !p.IsDeleted && (string.IsNullOrEmpty(search) || p.Name.Contains(search) || p.Code.Contains(search)),
            ct);
        return new PagedResult<ProjectDto>(items.Select(p => p.ToDto()).ToList(), total, request.Page, request.PageSize);
    }
}

public record GetProjectByIdQuery(Guid Id) : IRequest<ProjectDto>;

public class GetProjectByIdQueryHandler : IRequestHandler<GetProjectByIdQuery, ProjectDto>
{
    private readonly IProjectRepository _projects;

    public GetProjectByIdQueryHandler(IProjectRepository projects) => _projects = projects;

    public async Task<ProjectDto> Handle(GetProjectByIdQuery request, CancellationToken ct)
    {
        var project = await _projects.GetWithModulesAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(Project), request.Id);
        return project.ToDto();
    }
}
