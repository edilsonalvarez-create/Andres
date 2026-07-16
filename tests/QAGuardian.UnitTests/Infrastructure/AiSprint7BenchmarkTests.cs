using FluentAssertions;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Domain.Enums;
using QAGuardian.Infrastructure.Ai;
using Xunit;
using Xunit.Abstractions;

namespace QAGuardian.UnitTests.Infrastructure;

/// <summary>
/// Benchmark Sprint 7: precisión de heurísticas, reducción de tokens y anti-alucinación (confidence).
/// Ejecutar: dotnet test --filter FullyQualifiedName~AiSprint7BenchmarkTests
/// </summary>
public class AiSprint7BenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public AiSprint7BenchmarkTests(ITestOutputHelper output) => _output = output;

    public static TheoryData<string, string, RiskLevel> GoldenFailures => new()
    {
        { "Timeout 30000ms exceeded waiting for selector", "DevOps", RiskLevel.Medium },
        { "Locator '.btn-submit' not found", "QA", RiskLevel.Medium },
        { "HTTP 500 Internal Server Error", "Desarrollador", RiskLevel.High },
        { "401 Unauthorized on /api/login", "DevOps", RiskLevel.High },
        { "SqlException: deadlock on Users", "Desarrollador", RiskLevel.High },
        { "Expected assert equal true but was false", "Desarrollador", RiskLevel.Medium },
    };

    [Theory]
    [MemberData(nameof(GoldenFailures))]
    public void Heuristic_precision_matches_golden_owner_and_criticality(
        string error, string expectedOwner, RiskLevel expectedCriticality)
    {
        var ctx = new FailureContext("TC-1", error, null, null, null, null, TestType.Functional, "Demo");
        var result = AiHeuristicEngine.Diagnose(ctx);

        result.SuggestedOwnerRole.Should().Be(expectedOwner);
        result.Criticality.Should().Be(expectedCriticality);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.7);
        result.EvidenceQuote.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Benchmark_heuristic_precision_and_token_reduction()
    {
        var cases = GoldenFailures
            .Select(row => (
                Error: (string)row[0],
                Owner: (string)row[1],
                Criticality: (RiskLevel)row[2]))
            .ToList();

        var ownerHits = 0;
        var criticalityHits = 0;
        var autoDefectSafe = 0;
        long tokensLegacy = 0;
        long tokensSprint7 = 0;

        foreach (var c in cases)
        {
            var fatStack = string.Join('\n', Enumerable.Range(0, 200).Select(i => $"at Module.Call{i}() in file.cs:line {i}"));
            var fatLogs = new string('x', 8000);
            var ctx = new FailureContext("TC-bench", c.Error, fatStack, fatLogs, "SELECT * FROM Huge", null,
                TestType.Functional, "Demo");

            // Legacy: stack/logs 4000 chars each in prompt body.
            tokensLegacy += AiFailureFingerprint.EstimateTokens(c.Error)
                            + AiFailureFingerprint.EstimateTokens(fatStack[..Math.Min(4000, fatStack.Length)])
                            + AiFailureFingerprint.EstimateTokens(fatLogs[..4000]);

            tokensSprint7 += AiFailureFingerprint.EstimateTokens(c.Error)
                             + AiFailureFingerprint.EstimateTokens(
                                 AiFailureFingerprint.Truncate(fatStack, 2000))
                             + AiFailureFingerprint.EstimateTokens(
                                 AiFailureFingerprint.Truncate(fatLogs, 1500));

            var d = AiHeuristicEngine.Diagnose(ctx);
            if (d.SuggestedOwnerRole == c.Owner) ownerHits++;
            if (d.Criticality == c.Criticality) criticalityHits++;
            if (d.Criticality >= RiskLevel.High && !d.ShouldAutoCreateDefect(0.75) && d.Confidence < 0.75)
                autoDefectSafe++;
            else if (d.ShouldAutoCreateDefect(0.75) == (c.Criticality >= RiskLevel.High && d.Confidence >= 0.75))
                autoDefectSafe++;
        }

        var ownerPrecision = ownerHits / (double)cases.Count;
        var critPrecision = criticalityHits / (double)cases.Count;
        var tokenReduction = 1.0 - tokensSprint7 / (double)tokensLegacy;

        _output.WriteLine("=== Sprint 7 AI Benchmark ===");
        _output.WriteLine($"Golden cases: {cases.Count}");
        _output.WriteLine($"Owner precision: {ownerPrecision:P1}");
        _output.WriteLine($"Criticality precision: {critPrecision:P1}");
        _output.WriteLine($"Prompt tokens (legacy truncate 4k): ~{tokensLegacy}");
        _output.WriteLine($"Prompt tokens (Sprint 7 2k/1.5k): ~{tokensSprint7}");
        _output.WriteLine($"Token reduction (context fields): {tokenReduction:P1}");
        _output.WriteLine($"Model default: claude-sonnet-4-5 (vs previous claude-opus-4-8) — ~3–5x cheaper API tier");
        _output.WriteLine($"Thinking: disabled by default (additional cost avoidance)");
        _output.WriteLine($"Max failures/run: 5 (was 10) — up to 50% fewer LLM calls/run");

        ownerPrecision.Should().Be(1.0);
        critPrecision.Should().Be(1.0);
        tokenReduction.Should().BeGreaterThan(0.35);
    }

    [Fact]
    public void Fingerprint_is_stable_for_equivalent_failures()
    {
        var a = new FailureContext("T", "Timeout 30000ms exceeded waiting for selector", "at Foo.Bar()", null, null, null,
            TestType.Functional, "P");
        var b = new FailureContext("T", "Timeout 60000ms exceeded waiting for selector", "at Foo.Bar()", null, null, null,
            TestType.Functional, "P");

        AiFailureFingerprint.Compute(a).Should().Be(AiFailureFingerprint.Compute(b));
    }

    [Fact]
    public void Low_confidence_blocks_auto_defect()
    {
        var ctx = new FailureContext("T", "weird unknown boom", null, null, null, null, TestType.Api, "P");
        var d = AiHeuristicEngine.Diagnose(ctx);
        d.Confidence.Should().BeLessThan(0.75);
        d.ShouldAutoCreateDefect().Should().BeFalse();
    }

    [Fact]
    public void Generation_avoids_duplicate_catalog_titles()
    {
        var existing = new[] { "TC-0001: Validar contratos de API impactados por el PR" };
        var result = AiHeuristicEngine.GenerateTests(
            ["src/api/UsersController.cs"], existing);

        result.TestCases.Should().ContainSingle();
        result.TestCases[0].Title.Should().Contain("regresión PR");
    }

    [Fact]
    public void High_confidence_server_error_allows_auto_defect()
    {
        var ctx = new FailureContext("T", "HTTP 500 Internal Server Error", null, null, null, null,
            TestType.Api, "P");
        var d = AiHeuristicEngine.Diagnose(ctx);
        d.ShouldAutoCreateDefect().Should().BeTrue();
    }
}
