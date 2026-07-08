using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>Agregado raíz: caso de prueba manual o automatizado.</summary>
public class TestCase : AuditableEntity
{
    private readonly List<TestStep> _steps = [];

    private TestCase() { } // EF Core

    public TestCase(
        Guid projectId,
        string code,
        string title,
        TestType type,
        TestPriority priority,
        Guid? moduleId = null,
        Guid? userStoryId = null,
        string? preconditions = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("El código del caso de prueba es obligatorio.");
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("El título del caso de prueba es obligatorio.");
        ProjectId = projectId;
        Code = code.Trim().ToUpperInvariant();
        Title = title.Trim();
        Type = type;
        Priority = priority;
        ModuleId = moduleId;
        UserStoryId = userStoryId;
        Preconditions = preconditions;
        Status = TestCaseStatus.Draft;
        Framework = AutomationFramework.Manual;
    }

    public Guid ProjectId { get; private set; }
    public Guid? ModuleId { get; private set; }
    public Guid? UserStoryId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string? Preconditions { get; private set; }
    public TestType Type { get; private set; }
    public TestPriority Priority { get; private set; }
    public TestCaseStatus Status { get; private set; }
    public AutomationFramework Framework { get; private set; }
    /// <summary>Ruta del script de automatización (spec de Playwright, collection de Postman, plan de JMeter, etc.).</summary>
    public string? AutomationScriptPath { get; private set; }
    public string? Tags { get; private set; }

    public IReadOnlyCollection<TestStep> Steps => _steps.AsReadOnly();

    public void Update(string title, TestType type, TestPriority priority, string? preconditions, string? tags)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("El título del caso de prueba es obligatorio.");
        Title = title.Trim();
        Type = type;
        Priority = priority;
        Preconditions = preconditions;
        Tags = tags;
    }

    public void Automate(AutomationFramework framework, string scriptPath)
    {
        if (framework == AutomationFramework.Manual)
            throw new DomainException("Para automatizar debe indicar un framework distinto de Manual.");
        if (string.IsNullOrWhiteSpace(scriptPath))
            throw new DomainException("La ruta del script de automatización es obligatoria.");
        Framework = framework;
        AutomationScriptPath = scriptPath.Trim();
    }

    public TestStep AddStep(int order, string action, string expectedResult)
    {
        if (_steps.Any(s => s.Order == order))
            throw new DomainException($"Ya existe un paso con el orden {order}.");
        var step = new TestStep(Id, order, action, expectedResult);
        _steps.Add(step);
        return step;
    }

    public void ClearSteps() => _steps.Clear();
    public void Activate() => Status = TestCaseStatus.Active;
    public void Deprecate() => Status = TestCaseStatus.Deprecated;
}
