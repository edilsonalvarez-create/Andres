using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Enums;
using QAGuardian.Infrastructure.Runners;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

/// <summary>Sprint 13-C: runners cableados a <see cref="ISandboxedProcessExecutor"/>, no a ProcessExecutor.</summary>
public class RunnerSandboxWiringTests
{
    [Fact]
    public void Runners_de_usuario_no_exponen_constructor_con_ProcessExecutor()
    {
        typeof(PlaywrightTestRunner).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Should().NotContain(p => p.ParameterType == typeof(ProcessExecutor));

        typeof(NewmanTestRunner).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Should().Contain(p => p.ParameterType == typeof(ISandboxedProcessExecutor));

        typeof(JMeterTestRunner).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Should().Contain(p => p.ParameterType == typeof(ISandboxedProcessExecutor));

        typeof(ZapScanRunner).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Should().Contain(p => p.ParameterType == typeof(ISandboxedProcessExecutor));

        typeof(SeleniumIdeTestRunner).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Should().Contain(p => p.ParameterType == typeof(ISandboxedProcessExecutor));

        typeof(VisualRegressionRunner).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Should().Contain(p => p.ParameterType == typeof(ISandboxedProcessExecutor));

        typeof(PlaywrightRecorder).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Should().Contain(p => p.ParameterType == typeof(ISandboxedProcessExecutor));
    }

    [Fact]
    public async Task Zap_usa_sandbox_con_imagen_oficial_sin_PreferHostDockerCli()
    {
        var exec = Substitute.For<ISandboxedProcessExecutor>();
        exec.UsesContainerSandbox.Returns(true);
        exec.RunAsync(Arg.Any<ScriptExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ScriptExecutionResult(1, "", "fail", false, []));

        var ssrf = Substitute.For<ISsrfGuard>();
        ssrf.ValidateOutboundUri(Arg.Any<string?>())
            .Returns(Result<Uri>.Success(new Uri("https://example.com/")));

        var opts = Options.Create(new RunnerSandboxOptions
        {
            SandboxImageZap = "ghcr.io/zaproxy/zaproxy:stable"
        });
        var runner = new ZapScanRunner(exec, opts, ssrf);
        var work = Path.Combine(Path.GetTempPath(), "zap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            await runner.ExecuteAsync(new TestRunContext(
                Guid.NewGuid(), Guid.NewGuid(), TestType.Security,
                EnvironmentType.QA, work,
                [new TestScriptRef(null, "scan", "https://example.com")],
                new Dictionary<string, string>()), CancellationToken.None);

            await exec.Received(1).RunAsync(
                Arg.Is<ScriptExecutionRequest>(r =>
                    !r.PreferHostDockerCli
                    && r.FileName == "zap-baseline.py"
                    && r.ContainerImage == "ghcr.io/zaproxy/zaproxy:stable"
                    && r.Arguments.Contains("-t \"https://example.com/\"", StringComparison.Ordinal)),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Playwright_pasa_imagen_playwright_cuando_sandbox_activo()
    {
        var exec = Substitute.For<ISandboxedProcessExecutor>();
        exec.UsesContainerSandbox.Returns(true);
        exec.RunAsync(Arg.Any<ScriptExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ScriptExecutionResult(1, "", "", false, []));

        var opts = Options.Create(new RunnerSandboxOptions
        {
            SandboxImagePlaywright = "qaguardian/runner-playwright:local"
        });
        var runner = new PlaywrightTestRunner(exec, opts, NullLogger<PlaywrightTestRunner>.Instance);
        var work = Path.Combine(Path.GetTempPath(), "pw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            await runner.ExecuteAsync(new TestRunContext(
                Guid.NewGuid(), Guid.NewGuid(), TestType.Functional,
                EnvironmentType.QA, work,
                [new TestScriptRef(Guid.NewGuid(), "t", Path.Combine(work, "a.spec.ts"))],
                new Dictionary<string, string>()), CancellationToken.None);

            await exec.Received(1).RunAsync(
                Arg.Is<ScriptExecutionRequest>(r =>
                    r.ContainerImage == "qaguardian/runner-playwright:local"
                    && r.FileName == "npx"),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* ignore */ }
        }
    }
}
