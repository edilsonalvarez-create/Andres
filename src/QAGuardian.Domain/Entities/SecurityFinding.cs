using QAGuardian.Domain.Common;
using QAGuardian.Domain.Enums;

namespace QAGuardian.Domain.Entities;

/// <summary>Hallazgo de seguridad (OWASP ZAP, SonarQube) asociado a una ejecución.</summary>
public class SecurityFinding : BaseEntity
{
    private SecurityFinding() { } // EF Core

    public SecurityFinding(Guid testRunId, string name, RiskLevel risk, string category,
        string? url, string? parameter, string? evidence, string? solution, string? cweId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("El nombre del hallazgo es obligatorio.");
        TestRunId = testRunId;
        Name = name;
        Risk = risk;
        Category = category;
        Url = url;
        Parameter = parameter;
        Evidence = evidence;
        Solution = solution;
        CweId = cweId;
        DetectedAt = DateTime.UtcNow;
    }

    public Guid TestRunId { get; private set; }
    public string Name { get; private set; } = default!;
    public RiskLevel Risk { get; private set; }
    /// <summary>SQL Injection, XSS, Headers, Cookies, CSRF, etc.</summary>
    public string Category { get; private set; } = default!;
    public string? Url { get; private set; }
    public string? Parameter { get; private set; }
    public string? Evidence { get; private set; }
    public string? Solution { get; private set; }
    public string? CweId { get; private set; }
    public DateTime DetectedAt { get; private set; }
}
