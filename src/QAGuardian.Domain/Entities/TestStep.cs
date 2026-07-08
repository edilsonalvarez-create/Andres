using QAGuardian.Domain.Common;

namespace QAGuardian.Domain.Entities;

/// <summary>Paso individual de un caso de prueba.</summary>
public class TestStep : BaseEntity
{
    private TestStep() { } // EF Core

    public TestStep(Guid testCaseId, int order, string action, string expectedResult)
    {
        if (order <= 0) throw new DomainException("El orden del paso debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(action)) throw new DomainException("La acción del paso es obligatoria.");
        TestCaseId = testCaseId;
        Order = order;
        Action = action.Trim();
        ExpectedResult = expectedResult?.Trim() ?? string.Empty;
    }

    public Guid TestCaseId { get; private set; }
    public int Order { get; private set; }
    public string Action { get; private set; } = default!;
    public string ExpectedResult { get; private set; } = default!;
}
