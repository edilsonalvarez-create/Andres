using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.TestCases;

public record TestScriptDto(
    Guid TestCaseId, string Code, string Name, AutomationFramework Framework,
    TestType Type, string ScriptPath, string Content);

// ─────────────────────── Guardar/crear un script (autoría y edición) ───────────────────────

/// <summary>
/// Crea o actualiza un caso de prueba automatizado guardando el contenido de su script
/// (spec de Playwright, collection de Postman, plan de JMeter). Permite autoría y edición
/// desde el editor de la plataforma.
/// </summary>
public record SaveTestScriptCommand(
    Guid ProjectId,
    Guid? TestCaseId,
    string Name,
    AutomationFramework Framework,
    TestType Type,
    string Content,
    Guid? ModuleId = null) : IRequest<Result<TestScriptDto>>;

public class SaveTestScriptCommandValidator : AbstractValidator<SaveTestScriptCommand>
{
    public SaveTestScriptCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Framework).NotEqual(AutomationFramework.Manual)
            .WithMessage("El framework debe ser automatizado (Playwright, Postman, JMeter...).");
        RuleFor(x => x.Content).NotEmpty().WithMessage("El contenido del script es obligatorio.");
    }
}

public class SaveTestScriptCommandHandler : IRequestHandler<SaveTestScriptCommand, Result<TestScriptDto>>
{
    private readonly IProjectRepository _projects;
    private readonly ITestCaseRepository _testCases;
    private readonly IEvidenceStorage _storage;
    private readonly IProjectAccessService _access;
    private readonly IUnitOfWork _uow;

    public SaveTestScriptCommandHandler(IProjectRepository projects, ITestCaseRepository testCases,
        IEvidenceStorage storage, IProjectAccessService access, IUnitOfWork uow)
    {
        _projects = projects;
        _testCases = testCases;
        _storage = storage;
        _access = access;
        _uow = uow;
    }

    public async Task<Result<TestScriptDto>> Handle(SaveTestScriptCommand request, CancellationToken ct)
    {
        await _access.EnsureCanAccessProjectAsync(request.ProjectId, ct);
        _ = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), request.ProjectId);

        var extension = request.Framework switch
        {
            AutomationFramework.Playwright => ".spec.ts",
            AutomationFramework.Postman => ".postman_collection.json",
            AutomationFramework.JMeter => ".jmx",
            AutomationFramework.SeleniumIde => ".side",
            _ => ".txt"
        };
        var safeName = Sanitize(request.Name);
        var relativePath = await SaveContentAsync(
            $"scripts/{request.ProjectId:N}/{safeName}{extension}", request.Content, ct);
        var absolutePath = _storage.GetAbsolutePath(relativePath);

        TestCase testCase;
        if (request.TestCaseId is { } id)
        {
            testCase = await _testCases.GetByIdAsync(id, ct)
                ?? throw new NotFoundException(nameof(TestCase), id);
            testCase.Update(request.Name, request.Type, testCase.Priority, testCase.Preconditions, testCase.Tags);
            testCase.Automate(request.Framework, absolutePath);
            testCase.Activate();
        }
        else
        {
            var sequence = await _testCases.CountAsync(tc => tc.ProjectId == request.ProjectId, ct);
            var code = $"TC-SC-{sequence + 1:D4}";
            testCase = new TestCase(request.ProjectId, code, request.Name, request.Type,
                TestPriority.High, request.ModuleId);
            testCase.Automate(request.Framework, absolutePath);
            testCase.Activate();
            await _testCases.AddAsync(testCase, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return Result<TestScriptDto>.Success(new TestScriptDto(
            testCase.Id, testCase.Code, testCase.Title, testCase.Framework, testCase.Type,
            absolutePath, request.Content));
    }

    private async Task<string> SaveContentAsync(string relativePath, string content, CancellationToken ct)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        return await _storage.SaveAsync(relativePath, stream, ct);
    }

    private static string Sanitize(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars).Trim('-').ToLowerInvariant();
        return string.IsNullOrEmpty(slug) ? "script" : slug;
    }
}

// ─────────────────────── Leer el contenido de un script (para editar) ───────────────────────

public record GetTestScriptQuery(Guid TestCaseId) : IRequest<TestScriptDto>;

public class GetTestScriptQueryHandler : IRequestHandler<GetTestScriptQuery, TestScriptDto>
{
    private readonly ITestCaseRepository _testCases;
    private readonly IEvidenceStorage _storage;
    private readonly IProjectAccessService _access;

    public GetTestScriptQueryHandler(
        ITestCaseRepository testCases, IEvidenceStorage storage, IProjectAccessService access)
    {
        _testCases = testCases;
        _storage = storage;
        _access = access;
    }

    public async Task<TestScriptDto> Handle(GetTestScriptQuery request, CancellationToken ct)
    {
        var testCase = await _testCases.GetByIdAsync(request.TestCaseId, ct);
        if (testCase is null || !await _access.CanAccessProjectAsync(testCase.ProjectId, ct))
            throw new NotFoundException(nameof(TestCase), request.TestCaseId);
        if (testCase.AutomationScriptPath is null)
            throw new DomainException("El caso de prueba no tiene un script automatizado asociado.");

        var path = testCase.AutomationScriptPath;
        var content = await LoadScriptContentAsync(testCase, path, ct);

        return new TestScriptDto(testCase.Id, testCase.Code, testCase.Title, testCase.Framework,
            testCase.Type, path, content);
    }

    /// <summary>
    /// Devuelve el contenido editable del script. Si el "script" es en realidad una URL objetivo
    /// (casos visuales) o un archivo externo al almacenamiento gestionado (p. ej. un .jmx en disco),
    /// devuelve una observación explicativa en lugar de un cuadro vacío.
    /// </summary>
    private async Task<string> LoadScriptContentAsync(TestCase testCase, string path, CancellationToken ct)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return $"// Este caso ({testCase.Framework}) no tiene un script editable: apunta a una URL objetivo.\n" +
                   $"// URL: {path}\n" +
                   "// La plataforma captura/consulta esa URL al ejecutar; edita la URL desde \"Automatizar\".";
        }

        try
        {
            await using var stream = await _storage.OpenReadAsync(path, ct);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(ct);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException
            or UnauthorizedAccessException)
        {
            // El script vive fuera del almacenamiento gestionado por QA Guardian (o fue movido/eliminado).
            return $"// El script de este caso ({testCase.Framework}) no está almacenado dentro de QA Guardian:\n" +
                   $"// es un archivo externo en disco.\n" +
                   $"// Ruta: {path}\n" +
                   "// Edítalo en su ubicación original, o pega su contenido aquí y guarda para gestionarlo desde la plataforma.";
        }
    }
}

// ─────────────────────── Grabar una spec de Playwright (codegen) ───────────────────────

public record RecordPlaywrightScriptCommand(Guid ProjectId, string Url) : IRequest<Result<RecordedScript>>;

public class RecordPlaywrightScriptCommandValidator : AbstractValidator<RecordPlaywrightScriptCommand>
{
    public RecordPlaywrightScriptCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Url).NotEmpty()
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out _)).WithMessage("La URL no es válida.");
    }
}

public class RecordPlaywrightScriptCommandHandler
    : IRequestHandler<RecordPlaywrightScriptCommand, Result<RecordedScript>>
{
    private readonly IProjectRepository _projects;
    private readonly IPlaywrightRecorder _recorder;
    private readonly IEvidenceStorage _storage;
    private readonly IProjectAccessService _access;

    public RecordPlaywrightScriptCommandHandler(
        IProjectRepository projects, IPlaywrightRecorder recorder,
        IEvidenceStorage storage, IProjectAccessService access)
    {
        _projects = projects;
        _recorder = recorder;
        _storage = storage;
        _access = access;
    }

    public async Task<Result<RecordedScript>> Handle(RecordPlaywrightScriptCommand request, CancellationToken ct)
    {
        await _access.EnsureCanAccessProjectAsync(request.ProjectId, ct);
        _ = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), request.ProjectId);

        var workDir = _storage.GetAbsolutePath($"scripts/{request.ProjectId:N}");
        Directory.CreateDirectory(workDir);
        var recorded = await _recorder.RecordAsync(request.Url, workDir, ct);
        return Result<RecordedScript>.Success(recorded);
    }
}
