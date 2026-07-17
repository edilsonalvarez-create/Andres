using System.Text;
using System.Text.Json;
using FluentValidation;
using MediatR;
using QAGuardian.Application.Abstractions.Persistence;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Common;
using QAGuardian.Domain.Entities;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Features.Integrations;

public record PostmanImportResultDto(
    Guid TestCaseId,
    string TestCaseCode,
    string CollectionName,
    int RequestsImported,
    int VariablesImported,
    bool EnvironmentImported,
    string CollectionPath,
    string? EnvironmentPath);

/// <summary>
/// Importa una collection de Postman (y opcionalmente su environment): valida el JSON,
/// lo almacena, registra las variables y crea un caso de prueba automatizado de tipo API
/// listo para ejecutarse con Newman. Si hay environment, lo guarda en la integración
/// Postman del proyecto para usarlo en las ejecuciones.
/// </summary>
public record ImportPostmanCollectionCommand(
    Guid ProjectId,
    string CollectionJson,
    string? EnvironmentJson,
    Guid? ModuleId) : IRequest<Result<PostmanImportResultDto>>;

public class ImportPostmanCollectionCommandValidator : AbstractValidator<ImportPostmanCollectionCommand>
{
    public ImportPostmanCollectionCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.CollectionJson).NotEmpty().WithMessage("El contenido de la collection es obligatorio.");
    }
}

public class ImportPostmanCollectionCommandHandler
    : IRequestHandler<ImportPostmanCollectionCommand, Result<PostmanImportResultDto>>
{
    private readonly IProjectRepository _projects;
    private readonly ITestCaseRepository _testCases;
    private readonly IRepository<IntegrationSetting> _settings;
    private readonly IEvidenceStorage _storage;
    private readonly IProjectAccessService _access;
    private readonly IUnitOfWork _uow;

    public ImportPostmanCollectionCommandHandler(
        IProjectRepository projects, ITestCaseRepository testCases,
        IRepository<IntegrationSetting> settings, IEvidenceStorage storage,
        IProjectAccessService access, IUnitOfWork uow)
    {
        _projects = projects;
        _testCases = testCases;
        _settings = settings;
        _storage = storage;
        _access = access;
        _uow = uow;
    }

    public async Task<Result<PostmanImportResultDto>> Handle(
        ImportPostmanCollectionCommand request, CancellationToken ct)
    {
        await _access.EnsureCanAccessProjectAsync(request.ProjectId, ct);
        _ = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException(nameof(Project), request.ProjectId);

        // 1. Validar y analizar la collection.
        string collectionName;
        int requestCount;
        int collectionVariables;
        try
        {
            using var doc = JsonDocument.Parse(request.CollectionJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("info", out var info) || !root.TryGetProperty("item", out var items))
                return Result<PostmanImportResultDto>.Failure(
                    "El JSON no es una collection de Postman válida (faltan 'info' o 'item').");

            collectionName = info.TryGetProperty("name", out var name)
                ? name.GetString() ?? "Collection importada"
                : "Collection importada";
            requestCount = CountRequests(items);
            collectionVariables = root.TryGetProperty("variable", out var vars) && vars.ValueKind == JsonValueKind.Array
                ? vars.GetArrayLength() : 0;
        }
        catch (JsonException)
        {
            return Result<PostmanImportResultDto>.Failure("El contenido de la collection no es JSON válido.");
        }

        var safeName = Sanitize(collectionName);
        var folder = $"postman/{request.ProjectId:N}";

        // 2. Almacenar la collection.
        var collectionPath = await SaveJsonAsync(
            $"{folder}/{safeName}.postman_collection.json", request.CollectionJson, ct);

        // 3. Almacenar el environment (opcional) y contar sus variables.
        string? environmentPath = null;
        var environmentVariables = 0;
        if (!string.IsNullOrWhiteSpace(request.EnvironmentJson))
        {
            try
            {
                using var envDoc = JsonDocument.Parse(request.EnvironmentJson);
                if (envDoc.RootElement.TryGetProperty("values", out var values)
                    && values.ValueKind == JsonValueKind.Array)
                    environmentVariables = values.GetArrayLength();
            }
            catch (JsonException)
            {
                return Result<PostmanImportResultDto>.Failure("El environment no es JSON válido.");
            }

            environmentPath = await SaveJsonAsync(
                $"{folder}/{safeName}.postman_environment.json", request.EnvironmentJson, ct);
            await UpsertPostmanEnvironmentAsync(request.ProjectId, environmentPath, ct);
        }

        // 4. Registrar el caso de prueba automatizado (API + Postman).
        var sequence = await _testCases.CountAsync(tc => tc.ProjectId == request.ProjectId, ct);
        var code = $"TC-PM-{sequence + 1:D4}";
        var testCase = new TestCase(request.ProjectId, code, collectionName,
            TestType.Api, TestPriority.High, request.ModuleId,
            preconditions: $"Importado desde Postman. {requestCount} request(s).");
        testCase.Automate(AutomationFramework.Postman, _storage.GetAbsolutePath(collectionPath));
        testCase.Activate();
        await _testCases.AddAsync(testCase, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<PostmanImportResultDto>.Success(new PostmanImportResultDto(
            testCase.Id, code, collectionName, requestCount,
            collectionVariables + environmentVariables,
            environmentPath is not null, collectionPath, environmentPath));
    }

    /// <summary>Cuenta recursivamente los requests (ítems con nodo 'request') de la collection.</summary>
    private static int CountRequests(JsonElement items)
    {
        var count = 0;
        if (items.ValueKind != JsonValueKind.Array) return 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("request", out _)) count++;
            if (item.TryGetProperty("item", out var children)) count += CountRequests(children);
        }
        return count;
    }

    private async Task<string> SaveJsonAsync(string relativePath, string json, CancellationToken ct)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await _storage.SaveAsync(relativePath, stream, ct);
    }

    private async Task UpsertPostmanEnvironmentAsync(Guid projectId, string environmentPath, CancellationToken ct)
    {
        var extra = JsonSerializer.Serialize(new { postmanEnvironment = _storage.GetAbsolutePath(environmentPath) });
        var existing = (await _settings.ListAsync(
            s => s.ProjectId == projectId && s.Type == IntegrationType.Postman, ct)).FirstOrDefault();
        if (existing is not null)
            existing.Update(existing.BaseUrl, null, extra, true);
        else
            await _settings.AddAsync(new IntegrationSetting(projectId, IntegrationType.Postman, "", null, extra), ct);
    }

    private static string Sanitize(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var slug = new string(chars).Trim('-').ToLowerInvariant();
        return string.IsNullOrEmpty(slug) ? "collection" : slug;
    }
}
