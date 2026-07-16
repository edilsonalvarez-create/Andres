using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.TestCases;

public record TestStepDto(int Order, string Action, string ExpectedResult);

public record TestCaseDto(
    Guid Id, Guid ProjectId, Guid? ModuleId, Guid? UserStoryId, string Code, string Title,
    string? Preconditions, TestType Type, TestPriority Priority, TestCaseStatus Status,
    AutomationFramework Framework, string? AutomationScriptPath, string? Tags,
    IReadOnlyList<TestStepDto> Steps);

public static class TestCaseMapper
{
    public static TestCaseDto ToDto(this TestCase tc) => new(
        tc.Id, tc.ProjectId, tc.ModuleId, tc.UserStoryId, tc.Code, tc.Title, tc.Preconditions,
        tc.Type, tc.Priority, tc.Status, tc.Framework, tc.AutomationScriptPath, tc.Tags,
        tc.Steps.OrderBy(s => s.Order).Select(s => new TestStepDto(s.Order, s.Action, s.ExpectedResult)).ToList());
}

// ─────────────────────────── Crear caso de prueba ───────────────────────────

public record CreateTestCaseCommand(
    Guid ProjectId, string Code, string Title, TestType Type, TestPriority Priority,
    Guid? ModuleId, Guid? UserStoryId, string? Preconditions, string? Tags,
    List<TestStepDto> Steps) : IRequest<Result<TestCaseDto>>;

public class CreateTestCaseCommandValidator : AbstractValidator<CreateTestCaseCommand>
{
    public CreateTestCaseCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleForEach(x => x.Steps).ChildRules(step =>
        {
            step.RuleFor(s => s.Order).GreaterThan(0);
            step.RuleFor(s => s.Action).NotEmpty();
        });
    }
}

public class CreateTestCaseCommandHandler : IRequestHandler<CreateTestCaseCommand, Result<TestCaseDto>>
{
    private readonly ITestCaseRepository _testCases;
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _uow;

    public CreateTestCaseCommandHandler(ITestCaseRepository testCases, IProjectRepository projects, IUnitOfWork uow)
    {
        _testCases = testCases;
        _projects = projects;
        _uow = uow;
    }

    public async Task<Result<TestCaseDto>> Handle(CreateTestCaseCommand request, CancellationToken ct)
    {
        _ = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), request.ProjectId);

        var codeInUse = await _testCases.AnyAsync(
            tc => tc.ProjectId == request.ProjectId && tc.Code == request.Code.ToUpper() && !tc.IsDeleted, ct);
        if (codeInUse)
            return Result<TestCaseDto>.Failure($"Ya existe un caso de prueba con el código '{request.Code}' en el proyecto.");

        var testCase = new TestCase(request.ProjectId, request.Code, request.Title, request.Type,
            request.Priority, request.ModuleId, request.UserStoryId, request.Preconditions);
        foreach (var step in request.Steps.OrderBy(s => s.Order))
            testCase.AddStep(step.Order, step.Action, step.ExpectedResult);

        await _testCases.AddAsync(testCase, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<TestCaseDto>.Success(testCase.ToDto());
    }
}

// ─────────────────────────── Actualizar caso de prueba ───────────────────────────

public record UpdateTestCaseCommand(
    Guid Id, string Title, TestType Type, TestPriority Priority,
    string? Preconditions, string? Tags, List<TestStepDto> Steps) : IRequest<Result<TestCaseDto>>;

public class UpdateTestCaseCommandValidator : AbstractValidator<UpdateTestCaseCommand>
{
    public UpdateTestCaseCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
    }
}

public class UpdateTestCaseCommandHandler : IRequestHandler<UpdateTestCaseCommand, Result<TestCaseDto>>
{
    private readonly ITestCaseRepository _testCases;
    private readonly IUnitOfWork _uow;

    public UpdateTestCaseCommandHandler(ITestCaseRepository testCases, IUnitOfWork uow)
    {
        _testCases = testCases;
        _uow = uow;
    }

    public async Task<Result<TestCaseDto>> Handle(UpdateTestCaseCommand request, CancellationToken ct)
    {
        var testCase = await _testCases.GetWithStepsAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(TestCase), request.Id);

        testCase.Update(request.Title, request.Type, request.Priority, request.Preconditions, request.Tags);
        testCase.ClearSteps();
        foreach (var step in request.Steps.OrderBy(s => s.Order))
            testCase.AddStep(step.Order, step.Action, step.ExpectedResult);

        await _uow.SaveChangesAsync(ct);
        return Result<TestCaseDto>.Success(testCase.ToDto());
    }
}

// ─────────────────────────── Automatizar caso de prueba ───────────────────────────

public record AutomateTestCaseCommand(Guid Id, AutomationFramework Framework, string ScriptPath)
    : IRequest<Result<TestCaseDto>>;

public class AutomateTestCaseCommandValidator : AbstractValidator<AutomateTestCaseCommand>
{
    public AutomateTestCaseCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Framework).NotEqual(AutomationFramework.Manual);
        RuleFor(x => x.ScriptPath).NotEmpty().MaximumLength(500);
    }
}

public class AutomateTestCaseCommandHandler : IRequestHandler<AutomateTestCaseCommand, Result<TestCaseDto>>
{
    private readonly ITestCaseRepository _testCases;
    private readonly IUnitOfWork _uow;

    public AutomateTestCaseCommandHandler(ITestCaseRepository testCases, IUnitOfWork uow)
    {
        _testCases = testCases;
        _uow = uow;
    }

    public async Task<Result<TestCaseDto>> Handle(AutomateTestCaseCommand request, CancellationToken ct)
    {
        var testCase = await _testCases.GetWithStepsAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(TestCase), request.Id);
        testCase.Automate(request.Framework, request.ScriptPath);
        testCase.Activate();
        await _uow.SaveChangesAsync(ct);
        return Result<TestCaseDto>.Success(testCase.ToDto());
    }
}

// ─────────────────────────── Eliminar caso de prueba ───────────────────────────

public record DeleteTestCaseCommand(Guid Id) : IRequest<Result<bool>>;

public class DeleteTestCaseCommandHandler : IRequestHandler<DeleteTestCaseCommand, Result<bool>>
{
    private readonly ITestCaseRepository _testCases;
    private readonly IUnitOfWork _uow;

    public DeleteTestCaseCommandHandler(ITestCaseRepository testCases, IUnitOfWork uow)
    {
        _testCases = testCases;
        _uow = uow;
    }

    public async Task<Result<bool>> Handle(DeleteTestCaseCommand request, CancellationToken ct)
    {
        var testCase = await _testCases.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(TestCase), request.Id);
        testCase.IsDeleted = true;
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}

// ─────────────────────────── Consultas ───────────────────────────

public record GetTestCasesQuery(Guid ProjectId, int Page = 1, int PageSize = 20,
    TestType? Type = null, string? Search = null) : IRequest<PagedResult<TestCaseDto>>;

public class GetTestCasesQueryHandler : IRequestHandler<GetTestCasesQuery, PagedResult<TestCaseDto>>
{
    private readonly ITestCaseRepository _testCases;

    public GetTestCasesQueryHandler(ITestCaseRepository testCases) => _testCases = testCases;

    public async Task<PagedResult<TestCaseDto>> Handle(GetTestCasesQuery request, CancellationToken ct)
    {
        var search = request.Search?.Trim();
        var (items, total) = await _testCases.PagedAsync(request.Page, request.PageSize,
            tc => tc.ProjectId == request.ProjectId && !tc.IsDeleted
                && (request.Type == null || tc.Type == request.Type)
                && (string.IsNullOrEmpty(search) || tc.Title.Contains(search) || tc.Code.Contains(search)),
            ct);
        return new PagedResult<TestCaseDto>(items.Select(tc => tc.ToDto()).ToList(), total, request.Page, request.PageSize);
    }
}

public record GetTestCaseByIdQuery(Guid Id) : IRequest<TestCaseDto>;

public class GetTestCaseByIdQueryHandler : IRequestHandler<GetTestCaseByIdQuery, TestCaseDto>
{
    private readonly ITestCaseRepository _testCases;

    public GetTestCaseByIdQueryHandler(ITestCaseRepository testCases) => _testCases = testCases;

    public async Task<TestCaseDto> Handle(GetTestCaseByIdQuery request, CancellationToken ct)
    {
        var testCase = await _testCases.GetWithStepsAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(TestCase), request.Id);
        return testCase.ToDto();
    }
}

// ─────────────────────────── Trazabilidad: vincular historia ───────────────────────────

public record LinkTestCaseToUserStoryCommand(Guid Id, Guid? UserStoryId) : IRequest<Result<TestCaseDto>>;

public class LinkTestCaseToUserStoryCommandHandler
    : IRequestHandler<LinkTestCaseToUserStoryCommand, Result<TestCaseDto>>
{
    private readonly ITestCaseRepository _testCases;
    private readonly IUnitOfWork _uow;

    public LinkTestCaseToUserStoryCommandHandler(ITestCaseRepository testCases, IUnitOfWork uow)
    {
        _testCases = testCases;
        _uow = uow;
    }

    public async Task<Result<TestCaseDto>> Handle(LinkTestCaseToUserStoryCommand request, CancellationToken ct)
    {
        var testCase = await _testCases.GetWithStepsAsync(request.Id, ct)
            ?? throw new NotFoundException(nameof(TestCase), request.Id);
        testCase.LinkUserStory(request.UserStoryId);
        await _uow.SaveChangesAsync(ct);
        return Result<TestCaseDto>.Success(testCase.ToDto());
    }
}
