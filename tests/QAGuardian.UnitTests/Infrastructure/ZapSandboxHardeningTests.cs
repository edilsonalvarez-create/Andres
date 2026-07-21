using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Application.Common.Models;
using QAGuardian.Domain.Enums;
using QAGuardian.Infrastructure.Runners;
using QAGuardian.Infrastructure.Security;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

/// <summary>Sprint 19-A (B4): ZAP vía sandbox endurecido + targetUrl validado (sin PreferHostDockerCli).</summary>
public class ZapSandboxHardeningTests
{
    [Fact]
    public async Task Zap_se_lanza_con_flags_de_hardening_equivalentes()
    {
        ScriptExecutionRequest? captured = null;
        var exec = Substitute.For<ISandboxedProcessExecutor>();
        exec.UsesContainerSandbox.Returns(true);
        exec.RunAsync(Arg.Do<ScriptExecutionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new ScriptExecutionResult(1, "", "fail", false, []));

        var ssrf = Substitute.For<ISsrfGuard>();
        ssrf.ValidateOutboundUri(Arg.Any<string?>())
            .Returns(Result<Uri>.Success(new Uri("https://app.example.com/")));

        var opts = Options.Create(new RunnerSandboxOptions
        {
            SandboxImageZap = "ghcr.io/zaproxy/zaproxy:stable",
            SandboxUser = "1000:1000",
            SandboxNetworkMode = "bridge",
            SandboxMemoryLimit = "512m",
            SandboxCpus = "1.0",
            SandboxPidsLimit = 256,
            SandboxContainerWorkdir = "/workspace"
        });

        var runner = new ZapScanRunner(exec, opts, ssrf);
        var work = Path.Combine(Path.GetTempPath(), "zap-hard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            await runner.ExecuteAsync(CreateContext(work, "https://app.example.com/"), CancellationToken.None);

            captured.Should().NotBeNull();
            captured!.PreferHostDockerCli.Should().BeFalse();
            captured.FileName.Should().Be("zap-baseline.py");
            captured.ContainerImage.Should().Be("ghcr.io/zaproxy/zaproxy:stable");
            captured.Arguments.Should().StartWith("-t \"https://app.example.com/\"");

            var docker = CreateDockerExecutor(opts);
            var dockerArgs = docker.BuildDockerRunArguments(
                "qaguardian-run-zap-test",
                work,
                "/workspace",
                new Dictionary<string, string>(),
                captured.FileName,
                captured.Arguments,
                captured.ContainerImage);

            dockerArgs.Should().Contain("--rm");
            dockerArgs.Should().Contain("--user \"1000:1000\"");
            dockerArgs.Should().Contain("--cap-drop ALL");
            dockerArgs.Should().Contain("--read-only");
            dockerArgs.Should().Contain("--security-opt no-new-privileges");
            dockerArgs.Should().Contain("--memory \"512m\"");
            dockerArgs.Should().Contain("--cpus \"1.0\"");
            dockerArgs.Should().Contain("--pids-limit 256");
            dockerArgs.Should().Contain("--network \"bridge\"");
            dockerArgs.Should().Contain("\"ghcr.io/zaproxy/zaproxy:stable\"");
            dockerArgs.Should().Contain("\"zap-baseline.py\"");
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* ignore */ }
        }
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("https://169.254.169.254/")]
    [InlineData("https://10.0.0.5/internal")]
    [InlineData("https://192.168.1.10/admin")]
    [InlineData("https://127.0.0.1/")]
    public async Task Zap_rechaza_targetUrl_privado_o_metadata_antes_de_lanzar_contenedor(string targetUrl)
    {
        var exec = Substitute.For<ISandboxedProcessExecutor>();
        exec.UsesContainerSandbox.Returns(true);

        var runner = new ZapScanRunner(
            exec,
            Options.Create(new RunnerSandboxOptions()),
            CreateSsrfGuard());

        var work = Path.Combine(Path.GetTempPath(), "zap-ssrf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var outcome = await runner.ExecuteAsync(CreateContext(work, targetUrl), CancellationToken.None);

            outcome.Succeeded.Should().BeFalse();
            outcome.ErrorMessage.Should().Contain("rechazada");
            await exec.DidNotReceive()
                .RunAsync(Arg.Any<ScriptExecutionRequest>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Zap_targetUrl_con_inyeccion_de_flags_no_altera_comando_docker()
    {
        // Payload: comilla + flags docker. QuoteArg debe dejarlos dentro del token -t.
        const string injectedAbsolute = "https://example.com/\" --privileged --volume=/:/host";
        ScriptExecutionRequest? captured = null;
        var exec = Substitute.For<ISandboxedProcessExecutor>();
        exec.UsesContainerSandbox.Returns(true);
        exec.RunAsync(Arg.Do<ScriptExecutionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new ScriptExecutionResult(1, "", "fail", false, []));

        // AbsoluteUri canónico no conserva comillas; el runner cita urlCheck.Value.AbsoluteUri.
        // Para el path de inyección usamos el mismo QuoteArg que producción sobre el payload crudo
        // (equivalente a un AbsoluteUri malicioso post-validación).
        var ssrf = Substitute.For<ISsrfGuard>();
        ssrf.ValidateOutboundUri(Arg.Any<string?>())
            .Returns(Result<Uri>.Success(new Uri("https://example.com/--privileged")));

        var opts = Options.Create(new RunnerSandboxOptions
        {
            SandboxImageZap = "ghcr.io/zaproxy/zaproxy:stable",
            SandboxUser = "1000:1000",
            SandboxMemoryLimit = "512m"
        });

        var work = Path.Combine(Path.GetTempPath(), "zap-inj-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var runner = new ZapScanRunner(exec, opts, ssrf);
            await runner.ExecuteAsync(
                CreateContext(work, "https://example.com/--privileged"), CancellationToken.None);

            captured.Should().NotBeNull();
            captured!.PreferHostDockerCli.Should().BeFalse();
            captured.FileName.Should().Be("zap-baseline.py");
            captured.Arguments.Should().Contain("-t \"https://example.com/--privileged\"");

            // Payload con comilla: QuoteArg de producción deja flags solo dentro del token -t.
            var quotedMalicious = RunnerSandboxPaths.QuoteArg(injectedAbsolute);
            var zapArgsWithInject = $"-t {quotedMalicious} -J \"zap-report.json\" -I";
            var docker = CreateDockerExecutor(opts);
            var dockerArgs = docker.BuildDockerRunArguments(
                "qaguardian-run-zap-inj",
                work,
                "/workspace",
                new Dictionary<string, string>(),
                "zap-baseline.py",
                zapArgsWithInject,
                "ghcr.io/zaproxy/zaproxy:stable");

            dockerArgs.Should().Contain("--user \"1000:1000\"");
            dockerArgs.Should().Contain("--cap-drop ALL");
            dockerArgs.Should().Contain("--read-only");
            dockerArgs.Should().Contain("--security-opt no-new-privileges");
            dockerArgs.Should().Contain("--memory \"512m\"");

            var imageIdx = dockerArgs.IndexOf("\"ghcr.io/zaproxy/zaproxy:stable\"", StringComparison.Ordinal);
            imageIdx.Should().BeGreaterThan(0);
            var dockerFlags = dockerArgs[..imageIdx];
            dockerFlags.Should().NotContain("--privileged");
            dockerFlags.Should().NotContain("--volume=/:/host");
            dockerArgs.Should().Contain("-t " + quotedMalicious);

            // Path del runner: --privileged solo tras la imagen (argv de zap-baseline), no en flags docker.
            var runnerDockerArgs = docker.BuildDockerRunArguments(
                "qaguardian-run-zap-inj2",
                work,
                "/workspace",
                new Dictionary<string, string>(),
                captured.FileName,
                captured.Arguments,
                captured.ContainerImage);
            var runnerImageIdx = runnerDockerArgs.IndexOf(
                "\"ghcr.io/zaproxy/zaproxy:stable\"", StringComparison.Ordinal);
            runnerDockerArgs[..runnerImageIdx].Should().NotContain("--privileged");
            runnerDockerArgs[runnerImageIdx..].Should().Contain("--privileged");
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Zap_no_usa_PreferHostDockerCli()
    {
        ScriptExecutionRequest? captured = null;
        var exec = Substitute.For<ISandboxedProcessExecutor>();
        exec.UsesContainerSandbox.Returns(true);
        exec.RunAsync(Arg.Do<ScriptExecutionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new ScriptExecutionResult(1, "", "fail", false, []));

        var ssrf = Substitute.For<ISsrfGuard>();
        ssrf.ValidateOutboundUri(Arg.Any<string?>())
            .Returns(Result<Uri>.Success(new Uri("https://target.example/")));

        var runner = new ZapScanRunner(
            exec,
            Options.Create(new RunnerSandboxOptions { SandboxImageZap = "ghcr.io/zaproxy/zaproxy:stable" }),
            ssrf);

        var work = Path.Combine(Path.GetTempPath(), "zap-pref-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            await runner.ExecuteAsync(CreateContext(work, "https://target.example/"), CancellationToken.None);
            captured.Should().NotBeNull();
            captured!.PreferHostDockerCli.Should().BeFalse();
            captured.FileName.Should().Be("zap-baseline.py");
            captured.Arguments.Should().NotContain("docker run");
            captured.Arguments.Should().NotContain("PreferHostDockerCli");
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* ignore */ }
        }
    }

    private static TestRunContext CreateContext(string work, string targetUrl)
        => new(
            Guid.NewGuid(), Guid.NewGuid(), TestType.Security,
            EnvironmentType.QA, work,
            [new TestScriptRef(null, "scan", targetUrl)],
            new Dictionary<string, string> { ["targetUrl"] = targetUrl });

    private static SsrfGuard CreateSsrfGuard()
    {
        var dns = Substitute.For<IHostAddressResolver>();
        dns.GetAddresses(Arg.Any<string>()).Returns(new[] { IPAddress.Parse("93.184.216.34") });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:Ssrf:AllowHttpHosts:0"] = "",
            ["Security:Ssrf:AllowPrivateHosts:0"] = "",
        }).Build();
        return new SsrfGuard(dns, config, NullLogger<SsrfGuard>.Instance);
    }

    private static DockerSandboxedProcessExecutor CreateDockerExecutor(IOptions<RunnerSandboxOptions> opts)
    {
        var filter = new LocalSandboxedProcessExecutor(
            new ProcessExecutor(NullLogger<ProcessExecutor>.Instance),
            opts,
            NullLogger<LocalSandboxedProcessExecutor>.Instance);
        return new DockerSandboxedProcessExecutor(
            new ProcessExecutor(NullLogger<ProcessExecutor>.Instance),
            filter,
            opts,
            NullLogger<DockerSandboxedProcessExecutor>.Instance);
    }

}
