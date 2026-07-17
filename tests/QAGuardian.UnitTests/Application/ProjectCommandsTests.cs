using FluentAssertions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.Projects;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using Xunit;

namespace QAGuardian.UnitTests.Application;

public class ProjectCommandsTests
{
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    public ProjectCommandsTests()
    {
        _access.CanAccessProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _access.EnsureCanAccessProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Actualizar_cambia_nombre_y_persiste()
    {
        var project = new Project("ERP", "Nombre viejo", null, null);
        _projects.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        var handler = new UpdateProjectCommandHandler(_projects, _access, _uow);
        var result = await handler.Handle(
            new UpdateProjectCommand(project.Id, "Nombre nuevo", "desc", "https://git/x"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Nombre nuevo");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Actualizar_proyecto_inexistente_lanza_NotFound()
    {
        _projects.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Project?)null);
        var handler = new UpdateProjectCommandHandler(_projects, _access, _uow);

        var act = () => handler.Handle(new UpdateProjectCommand(Guid.NewGuid(), "X", null, null), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Actualizar_proyecto_ajeno_lanza_NotFound()
    {
        _access.EnsureCanAccessProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new NotFoundException(nameof(Project), Guid.NewGuid()));

        var handler = new UpdateProjectCommandHandler(_projects, _access, _uow);
        var act = () => handler.Handle(new UpdateProjectCommand(Guid.NewGuid(), "X", null, null), default);

        await act.Should().ThrowAsync<NotFoundException>();
        await _projects.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Eliminar_marca_borrado_y_desactiva()
    {
        var project = new Project("ERP", "Nombre", null, null);
        project.IsActive.Should().BeTrue();
        _projects.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        var handler = new DeleteProjectCommandHandler(_projects, _access, _uow);
        var result = await handler.Handle(new DeleteProjectCommand(project.Id), default);

        result.IsSuccess.Should().BeTrue();
        project.IsDeleted.Should().BeTrue();
        project.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Agregar_modulo_lo_devuelve_con_id()
    {
        var project = new Project("ERP", "Nombre", null, null);
        _projects.GetWithModulesAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);

        var handler = new AddModuleCommandHandler(_projects, _access, _uow);
        var result = await handler.Handle(new AddModuleCommand(project.Id, "Autenticación", "login/logout"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Autenticación");
        result.Value.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Agregar_modulo_a_proyecto_inexistente_lanza_NotFound()
    {
        _projects.GetWithModulesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Project?)null);
        var handler = new AddModuleCommandHandler(_projects, _access, _uow);

        var act = () => handler.Handle(new AddModuleCommand(Guid.NewGuid(), "X", null), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Listar_solo_proyectos_accesibles()
    {
        var visible = new Project("ERP", "Sistema ERP", null, null);
        var hidden = new Project("HR", "RRHH", null, null);
        _access.ListAccessibleProjectIdsAsync(Arg.Any<CancellationToken>())
            .Returns([visible.Id]);

        _projects.PagedAsync(1, 20, Arg.Any<System.Linq.Expressions.Expression<Func<Project, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var predicate = callInfo.ArgAt<System.Linq.Expressions.Expression<Func<Project, bool>>>(2);
                var compiled = predicate.Compile();
                var items = new[] { visible, hidden }.Where(compiled).ToList();
                return (items, items.Count);
            });

        var handler = new GetProjectsQueryHandler(_projects, _access);
        var result = await handler.Handle(new GetProjectsQuery(1, 20, null), default);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Code.Should().Be("ERP");
    }

    [Fact]
    public async Task GetById_ajeno_lanza_NotFound()
    {
        _access.EnsureCanAccessProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new NotFoundException(nameof(Project), Guid.NewGuid()));

        var handler = new GetProjectByIdQueryHandler(_projects, _access);
        var act = () => handler.Handle(new GetProjectByIdQuery(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
