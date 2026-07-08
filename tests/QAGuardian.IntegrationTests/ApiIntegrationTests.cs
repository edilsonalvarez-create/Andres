using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace QAGuardian.IntegrationTests;

/// <summary>Levanta la API completa con SQLite y Hangfire en memoria.</summary>
public class QAGuardianApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"qaguardian-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");
        builder.UseSetting("Database:UseSqlite", "true");
        builder.UseSetting("Hangfire:UseInMemory", "true");
        builder.UseSetting("Jwt:SigningKey", "clave-solo-para-pruebas-integracion-0123456789ABC");
        builder.UseSetting("Security:EncryptionKey", "clave-cifrado-solo-para-pruebas-integracion");
        builder.UseSetting("Seed:AdminEmail", "admin@qaguardian.test");
        builder.UseSetting("Seed:AdminPassword", "Admin.Pruebas.2026!");
        builder.UseSetting("Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore", "Information");
        builder.UseSetting("Database:EnableSensitiveLogging", "true");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* mejor esfuerzo */ }
    }
}

public class ApiIntegrationTests : IClassFixture<QAGuardianApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly QAGuardianApiFactory _factory;

    public ApiIntegrationTests(QAGuardianApiFactory factory) => _factory = factory;

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@qaguardian.test",
            password = "Admin.Pruebas.2026!"
        });
        login.EnsureSuccessStatusCode();
        var payload = await login.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var token = payload.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Health_responde_healthy()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_con_credenciales_invalidas_devuelve_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@qaguardian.test",
            password = "ContrasenaIncorrecta1!"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Endpoints_protegidos_requieren_token()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/projects");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Flujo_completo_proyecto_caso_de_prueba_y_dashboard()
    {
        var client = await CreateAuthenticatedClientAsync();

        // 1. Crear proyecto
        var createProject = await client.PostAsJsonAsync("/api/v1/projects", new
        {
            code = $"IT{Random.Shared.Next(1000, 9999)}",
            name = "Proyecto de integración",
            description = "Creado por pruebas de integración",
            repositoryUrl = "https://github.com/sumimedical/demo"
        });
        createProject.StatusCode.Should().Be(HttpStatusCode.OK);
        var project = await createProject.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var projectId = project.GetProperty("id").GetGuid();
        project.GetProperty("qualityGateId").ValueKind.Should().NotBe(JsonValueKind.Null,
            "el gate por defecto debe asignarse automáticamente");

        // 2. Crear caso de prueba con pasos
        var createCase = await client.PostAsJsonAsync("/api/v1/testcases", new
        {
            projectId,
            code = "TC-0001",
            title = "Login exitoso",
            type = 1,
            priority = 3,
            steps = new[]
            {
                new { order = 1, action = "Abrir login", expectedResult = "Formulario visible" },
                new { order = 2, action = "Ingresar credenciales", expectedResult = "Redirige al dashboard" }
            }
        });
        createCase.StatusCode.Should().Be(HttpStatusCode.OK);
        var testCase = await createCase.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        testCase.GetProperty("steps").GetArrayLength().Should().Be(2);

        // 3. Listar casos del proyecto
        var list = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/testcases?projectId={projectId}", JsonOptions);
        list.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);

        // 4. Dashboard responde con KPIs
        var dashboard = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/dashboard?projectId={projectId}", JsonOptions);
        dashboard.GetProperty("totalTestCases").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Validacion_devuelve_400_con_detalle_de_errores()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/v1/projects", new
        {
            code = "", // inválido
            name = ""
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("errors").ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task QualityGates_lista_el_gate_por_defecto()
    {
        var client = await CreateAuthenticatedClientAsync();
        var gates = await client.GetFromJsonAsync<JsonElement>("/api/v1/qualitygates", JsonOptions);
        gates.EnumerateArray().Should().Contain(g => g.GetProperty("isDefault").GetBoolean());
    }
}
