using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QAGuardian.Application.Abstractions.Services;
using QAGuardian.Infrastructure.Runners;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

public class RunnerSandboxSeamsTests
{
    [Fact]
    public void LocalExecutor_filtra_env_por_allowlist_y_bloquea_secretos()
    {
        var opts = Options.Create(new RunnerSandboxOptions
        {
            UseSandbox = false,
            EnvironmentAllowlistPrefixes = ["QA_GUARDIAN_", "PLAYWRIGHT_", "PATH"]
        });
        var sut = new LocalSandboxedProcessExecutor(
            new ProcessExecutor(NullLogger<ProcessExecutor>.Instance),
            opts,
            NullLogger<LocalSandboxedProcessExecutor>.Instance);

        var filtered = sut.FilterEnvironment(new Dictionary<string, string>
        {
            ["QA_GUARDIAN_ENV"] = "QA",
            ["PLAYWRIGHT_JSON_OUTPUT_NAME"] = "out.json",
            ["PATH"] = "/usr/bin",
            ["ConnectionStrings__DefaultConnection"] = "Server=evil",
            ["Jwt__SigningKey"] = "secret",
            ["RANDOM_TOOL"] = "nope"
        });

        filtered.Keys.Should().BeEquivalentTo("QA_GUARDIAN_ENV", "PLAYWRIGHT_JSON_OUTPUT_NAME", "PATH");
        filtered.Should().NotContainKey("ConnectionStrings__DefaultConnection");
        filtered.Should().NotContainKey("Jwt__SigningKey");
        filtered.Should().NotContainKey("RANDOM_TOOL");
    }

    [Fact]
    public void DockerSandbox_args_no_montan_secretos_ni_heredan_env_de_plataforma()
    {
        var opts = Options.Create(new RunnerSandboxOptions
        {
            UseSandbox = true,
            SandboxImage = "qaguardian/runner-newman:local",
            SandboxNetworkMode = "none",
            SandboxUser = "1000:1000",
            SandboxMemoryLimit = "512m",
            SandboxCpus = "1.0",
            SandboxPidsLimit = 256
        });
        var filter = new LocalSandboxedProcessExecutor(
            new ProcessExecutor(NullLogger<ProcessExecutor>.Instance),
            opts,
            NullLogger<LocalSandboxedProcessExecutor>.Instance);
        var sut = new DockerSandboxedProcessExecutor(
            new ProcessExecutor(NullLogger<ProcessExecutor>.Instance),
            filter,
            opts,
            NullLogger<DockerSandboxedProcessExecutor>.Instance);

        var env = filter.FilterEnvironment(new Dictionary<string, string>
        {
            ["QA_GUARDIAN_ENV"] = "QA",
            ["Jwt__SigningKey"] = "SHOULD_NOT_APPEAR",
            ["ConnectionStrings__DefaultConnection"] = "Server=secret"
        });

        var workdir = Path.Combine(Path.GetTempPath(), "qg-sandbox-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workdir);
        try
        {
            var args = sut.BuildDockerRunArguments(
                "qaguardian-run-abc",
                workdir,
                "/workspace",
                env,
                "newman",
                "run collection.json");

            args.Should().Contain("--network \"none\"");
            args.Should().Contain("--user \"1000:1000\"");
            args.Should().Contain("--read-only");
            args.Should().Contain("--cap-drop ALL");
            args.Should().Contain("--memory \"512m\"");
            args.Should().Contain("--cpus \"1.0\"");
            args.Should().Contain("--pids-limit 256");
            args.Should().Contain("-v ");
            args.Should().Contain("/workspace");
            args.Should().NotContain("Jwt__SigningKey");
            args.Should().NotContain("SHOULD_NOT_APPEAR");
            args.Should().NotContain("ConnectionStrings");
            args.Should().NotContain("/app");
            args.Should().Contain("-e \"QA_GUARDIAN_ENV=QA\"");
            args.Should().Contain("qaguardian/runner-newman:local");
        }
        finally
        {
            try { Directory.Delete(workdir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void NormalizeNetwork_default_none_y_host_rechazado()
    {
        DockerSandboxedProcessExecutor.NormalizeNetwork(null).Should().Be("none");
        DockerSandboxedProcessExecutor.NormalizeNetwork("").Should().Be("none");
        DockerSandboxedProcessExecutor.NormalizeNetwork("  ").Should().Be("none");
        DockerSandboxedProcessExecutor.NormalizeNetwork("none").Should().Be("none");
        DockerSandboxedProcessExecutor.NormalizeNetwork("bridge").Should().Be("bridge");

        var actHost = () => DockerSandboxedProcessExecutor.NormalizeNetwork("host");
        actHost.Should().Throw<InvalidOperationException>()
            .WithMessage("*host*vetado*");

        var actOther = () => DockerSandboxedProcessExecutor.NormalizeNetwork("container:foo");
        actOther.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResolveNetwork_usa_SandboxNetworkName_solo_en_bridge()
    {
        DockerSandboxedProcessExecutor.ResolveNetwork("none", "qg-egress").Should().Be("none");
        DockerSandboxedProcessExecutor.ResolveNetwork("bridge", null).Should().Be("bridge");
        DockerSandboxedProcessExecutor.ResolveNetwork("bridge", "  qg-egress  ")
            .Should().Be("qg-egress");
    }

    [Fact]
    public void RewritePaths_reemplaza_workdir_host_por_container()
    {
        var host = Path.Combine(Path.GetTempPath(), "run123");
        var rewritten = DockerSandboxedProcessExecutor.RewritePathsForContainer(
            $"run \"{host}{Path.DirectorySeparatorChar}col.json\" -r json --reporter-json-export \"{host}{Path.DirectorySeparatorChar}out.json\"",
            host,
            "/workspace");

        rewritten.Should().Contain("/workspace");
        rewritten.Should().NotContain(host);
    }

    [Fact]
    public async Task LocalWorkspace_crea_y_limpia_directorio_efimero()
    {
        var root = Path.Combine(Path.GetTempPath(), "qg-ws-" + Guid.NewGuid().ToString("N"));
        var host = new FakeHostEnvironment { ContentRootPath = Path.GetTempPath() };
        var opts = Options.Create(new RunnerSandboxOptions { WorkspaceRoot = root });
        var env = new LocalScriptExecutionEnvironment(
            opts, host, NullLogger<LocalScriptExecutionEnvironment>.Instance);

        var runId = Guid.NewGuid();
        await using (var ws = await env.CreateWorkspaceAsync(runId))
        {
            Directory.Exists(ws.RootPath).Should().BeTrue();
            await ws.WriteAllTextAsync("script.spec.ts", "test('x', () => {});");
            File.Exists(Path.Combine(ws.RootPath, "script.spec.ts")).Should().BeTrue();
            File.WriteAllText(Path.Combine(ws.RootPath, "shot.png"), "fake");
            ws.CollectArtifacts(["**/*.png"]).Should().ContainSingle(p => p.EndsWith("shot.png"));
        }

        // Tras dispose el workspace concreto se elimina; la raíz puede quedar vacía.
        Directory.Exists(root).Should().BeTrue();
    }

    private sealed class FakeHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "QAGuardian.Tests";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
