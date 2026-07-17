using System.Linq.Expressions;
using FluentAssertions;
using NSubstitute;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Features.Catalog;
using QAGuardian.Domain.Entities;
using Xunit;

namespace QAGuardian.UnitTests.Application;

public class CatalogHandlerTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IProjectAccessService _access = Substitute.For<IProjectAccessService>();

    public CatalogHandlerTests()
    {
        _access.CanAccessProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _access.EnsureCanAccessProjectAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Crear_requerimiento_valido()
    {
        var modules = Substitute.For<IRepository<Module>>();
        var requirements = Substitute.For<IRepository<Requirement>>();
        var projectId = Guid.NewGuid();
        var moduleId = Guid.NewGuid();
        modules.GetByIdAsync(moduleId, Arg.Any<CancellationToken>()).Returns(new Module(projectId, "Auth", null));
        requirements.AnyAsync(Arg.Any<Expression<Func<Requirement, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = new CreateRequirementCommandHandler(modules, requirements, _access, _uow);
        var result = await handler.Handle(
            new CreateRequirementCommand(moduleId, "req-1", "Login seguro", "desc"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("REQ-1");
        await requirements.Received(1).AddAsync(Arg.Any<Requirement>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_requerimiento_con_codigo_duplicado_falla()
    {
        var modules = Substitute.For<IRepository<Module>>();
        var requirements = Substitute.For<IRepository<Requirement>>();
        var moduleId = Guid.NewGuid();
        modules.GetByIdAsync(moduleId, Arg.Any<CancellationToken>()).Returns(new Module(Guid.NewGuid(), "Auth", null));
        requirements.AnyAsync(Arg.Any<Expression<Func<Requirement, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new CreateRequirementCommandHandler(modules, requirements, _access, _uow);
        var result = await handler.Handle(
            new CreateRequirementCommand(moduleId, "REQ-1", "Otro", null), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("REQ-1");
    }

    [Fact]
    public async Task Crear_historia_valida()
    {
        var requirements = Substitute.For<IRepository<Requirement>>();
        var modules = Substitute.For<IRepository<Module>>();
        var stories = Substitute.For<IRepository<UserStory>>();
        var projectId = Guid.NewGuid();
        var moduleId = Guid.NewGuid();
        var reqId = Guid.NewGuid();
        requirements.GetByIdAsync(reqId, Arg.Any<CancellationToken>())
            .Returns(new Requirement(moduleId, "REQ-1", "T", null));
        modules.GetByIdAsync(moduleId, Arg.Any<CancellationToken>())
            .Returns(new Module(projectId, "Auth", null));

        var handler = new CreateUserStoryCommandHandler(requirements, modules, stories, _access, _uow);
        var result = await handler.Handle(
            new CreateUserStoryCommand(reqId, "Como usuario quiero...", "Dado... cuando... entonces..."), default);

        result.IsSuccess.Should().BeTrue();
        await stories.Received(1).AddAsync(Arg.Any<UserStory>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Crear_version_duplicada_falla()
    {
        var projects = Substitute.For<IProjectRepository>();
        var versions = Substitute.For<IRepository<ProjectVersion>>();
        var projectId = Guid.NewGuid();
        projects.GetByIdAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(new Project("P", "Proyecto", null, null));
        versions.AnyAsync(Arg.Any<Expression<Func<ProjectVersion, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new CreateProjectVersionCommandHandler(projects, versions, _access, _uow);
        var result = await handler.Handle(new CreateProjectVersionCommand(projectId, "1.0.0", null), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("1.0.0");
    }

    [Fact]
    public async Task Marcar_version_como_liberada()
    {
        var versions = Substitute.For<IRepository<ProjectVersion>>();
        var version = new ProjectVersion(Guid.NewGuid(), "1.0.0", null);
        versions.GetByIdAsync(version.Id, Arg.Any<CancellationToken>()).Returns(version);

        var handler = new ReleaseProjectVersionCommandHandler(versions, _access, _uow);
        var result = await handler.Handle(new ReleaseProjectVersionCommand(version.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ReleasedAt.Should().NotBeNull();
    }
}
