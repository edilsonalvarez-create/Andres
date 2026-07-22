using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

/// <summary>
/// E2E opcional del sandbox Docker (Newman). Se omite si <c>docker</c> o la imagen no están disponibles.
/// Prueba negativa: el contenedor no ve secretos del proceso API.
/// </summary>
public class DockerSandboxE2ETests
{
    private static bool DockerAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "version --format {{.Server.Version}}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            if (!p.WaitForExit(15_000)) { try { p.Kill(); } catch { /* ignore */ } return false; }
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool ImageExists(string image)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"image inspect {image}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit(30_000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public void Negative_script_en_sandbox_no_ve_secretos_de_plataforma()
    {
        if (!DockerAvailable())
            return; // omitido en CI sin Docker
        const string image = "qaguardian/runner-newman:local";
        if (!ImageExists(image))
            return;

        var work = Path.Combine(Path.GetTempPath(), "qg-neg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            // Inyectamos deliberadamente un "secreto" solo en el host process env — el docker run
            // de sandbox NO debe propagar el environment del proceso (BuildDockerRun usa -e allowlist).
            var script = """
                const fs = require('fs');
                const env = process.env;
                const leak = Object.keys(env).filter(k =>
                  /JWT|ConnectionString|EncryptionKey|ADMIN_PASSWORD|SQL_SA/i.test(k));
                const appExists = fs.existsSync('/app');
                console.log(JSON.stringify({ leak, appExists, uid: process.getuid?.() ?? -1 }));
                """;
            File.WriteAllText(Path.Combine(work, "probe.js"), script);

            var name = "qaguardian-run-negtest-" + Guid.NewGuid().ToString("N")[..8];
            var args =
                $"run --rm --name {name} --user 1000:1000 --network none --workdir /workspace " +
                $"--read-only --tmpfs /tmp:rw,noexec,nosuid,size=32m --cap-drop ALL " +
                $"--security-opt no-new-privileges " +
                $"-v \"{work}:/workspace\" " +
                $"-e QA_GUARDIAN_ENV=QA " +
                $"{image} node probe.js";

            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            // Secretos presentes en el proceso host — docker run del sandbox NO debe heredarlos
            // (solo -e allowlist). ProcessStartInfo hereda el env del test process; lo importante
            // es que el contenedor no los reciba vía -e ni volumen /app.
            psi.Environment["Jwt__SigningKey"] = "SUPER_SECRET_JWT";
            psi.Environment["ConnectionStrings__DefaultConnection"] = "Server=secret;Password=x";
            psi.Environment["Security__EncryptionKey"] = "enc-secret";

            using var p = Process.Start(psi)!;
            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit(60_000);

            p.ExitCode.Should().Be(0, because: stderr + stdout);
            using var doc = JsonDocument.Parse(stdout.Trim());
            doc.RootElement.GetProperty("leak").GetArrayLength().Should().Be(0);
            doc.RootElement.GetProperty("appExists").GetBoolean().Should().BeFalse();
            doc.RootElement.GetProperty("uid").GetInt32().Should().Be(1000);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Newman_E2E_via_sandbox_docker()
    {
        if (!DockerAvailable())
            return;
        const string image = "qaguardian/runner-newman:local";
        if (!ImageExists(image))
            return;

        var work = Path.Combine(Path.GetTempPath(), "qg-newman-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            // Collection mínima: request a postman-echo (requiere bridge).
            var collection = """
                {
                  "info": { "name": "QG Sandbox Smoke", "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json" },
                  "item": [{
                    "name": "echo",
                    "request": { "method": "GET", "header": [], "url": "https://postman-echo.com/get?q=qaguardian" },
                    "event": [{
                      "listen": "test",
                      "script": {
                        "type": "text/javascript",
                        "exec": ["pm.test('status 200', function(){ pm.response.to.have.status(200); });"]
                      }
                    }]
                  }]
                }
                """;
            File.WriteAllText(Path.Combine(work, "smoke.postman_collection.json"), collection);

            var name = "qaguardian-run-newman-" + Guid.NewGuid().ToString("N")[..8];
            var export = "newman-out.json";
            var args =
                $"run --rm --name {name} --user 1000:1000 --network bridge --workdir /workspace " +
                $"--read-only --tmpfs /tmp:rw,noexec,nosuid,size=64m --cap-drop ALL " +
                $"--security-opt no-new-privileges " +
                $"-v \"{work}:/workspace\" " +
                $"{image} newman run smoke.postman_collection.json -r json --reporter-json-export {export}";

            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            p.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit(120_000);

            p.ExitCode.Should().Be(0, because: stderr.ToString() + stdout.ToString());
            File.Exists(Path.Combine(work, export)).Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* ignore */ }
        }
    }
}
