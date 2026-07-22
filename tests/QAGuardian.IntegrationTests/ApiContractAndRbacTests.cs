using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace QAGuardian.IntegrationTests;

/// <summary>
/// Pruebas de API sobre HTTP real (WebApplicationFactory): control de acceso por rol (RBAC),
/// contrato OpenAPI, y flujos de negocio de gate y defectos de punta a punta.
/// </summary>
public class ApiContractAndRbacTests : IClassFixture<QAGuardianApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly QAGuardianApiFactory _factory;

    public ApiContractAndRbacTests(QAGuardianApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "admin@qaguardian.test",
            password = "Admin.Pruebas.2026!"
        });
        login.EnsureSuccessStatusCode();
        var payload = await login.Content.ReadFromJsonAsync<JsonElement>(Json);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", payload.GetProperty("accessToken").GetString());
        return client;
    }

    // ── Contrato OpenAPI ────────────────────────────────────────────────

    [Fact]
    public async Task Swagger_expone_un_documento_OpenAPI_con_los_recursos_clave()
    {
        var client = _factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json", Json);

        doc.GetProperty("openapi").GetString().Should().StartWith("3.");
        // Las claves de path usan el casing del nombre del controlador; comparamos sin distinción.
        var pathNames = doc.GetProperty("paths").EnumerateObject().Select(p => p.Name.ToLowerInvariant()).ToList();
        pathNames.Should().Contain(p => p.Contains("/projects"));
        pathNames.Should().Contain(p => p.Contains("/qualitygates"));
        pathNames.Should().Contain(p => p.Contains("/testcases"));
        // El esquema de seguridad Bearer debe estar declarado (contrato de autenticación).
        doc.GetProperty("components").GetProperty("securitySchemes")
            .TryGetProperty("Bearer", out _).Should().BeTrue();
    }

    // ── RBAC: un rol de solo lectura no puede gestionar proyectos ───────

    [Fact]
    public async Task Usuario_rol_Cliente_no_puede_crear_proyectos_403()
    {
        var admin = await AdminClientAsync();
        var email = $"cliente{Guid.NewGuid():N}@qaguardian.test";

        // El admin registra un usuario con rol de solo lectura.
        var register = await admin.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            fullName = "Cliente Lectura",
            password = "Cliente.Pass.2026",
            roles = new[] { "Cliente" }
        });
        register.StatusCode.Should().Be(HttpStatusCode.OK);

        // Ese usuario inicia sesión y obtiene su propio token.
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Cliente.Pass.2026" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Puede leer (ViewReports = autenticado) pero no gestionar (ManageProjects).
        (await client.GetAsync("/api/v1/qualitygates")).StatusCode.Should().Be(HttpStatusCode.OK);
        var create = await client.PostAsJsonAsync("/api/v1/projects", new { code = "RBAC1", name = "No permitido" });
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Flujo de Quality Gate: crear → asignar ──────────────────────────

    [Fact]
    public async Task Crear_gate_y_asignarlo_a_un_proyecto()
    {
        var admin = await AdminClientAsync();

        var createGate = await admin.PostAsJsonAsync("/api/v1/qualitygates", new
        {
            name = $"Gate {Guid.NewGuid():N}",
            isDefault = false,
            conditions = new[]
            {
                new { metric = 1, @operator = 1, threshold = 90m, isBlocking = true },   // PassRate >= 90 (bloqueante)
                new { metric = 2, @operator = 1, threshold = 70m, isBlocking = false }    // Coverage >= 70 (advertencia)
            }
        });
        createGate.StatusCode.Should().Be(HttpStatusCode.OK);
        var gateId = (await createGate.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var createProject = await admin.PostAsJsonAsync("/api/v1/projects", new
        {
            code = $"GT{Random.Shared.Next(1000, 9999)}",
            name = "Proyecto con gate"
        });
        var projectId = (await createProject.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var assign = await admin.PostAsJsonAsync("/api/v1/qualitygates/assign", new { projectId, qualityGateId = gateId });
        assign.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Flujo de defecto: crear → transición de estado ──────────────────

    [Fact]
    public async Task Crear_defecto_y_asignarlo_via_API()
    {
        var admin = await AdminClientAsync();
        var createProject = await admin.PostAsJsonAsync("/api/v1/projects", new
        {
            code = $"DF{Random.Shared.Next(1000, 9999)}",
            name = "Proyecto con defectos"
        });
        var projectId = (await createProject.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        var createDefect = await admin.PostAsJsonAsync("/api/v1/defects", new
        {
            projectId,
            title = "Checkout lanza 500",
            description = "NullReference al pagar",
            severity = 4,   // Critical
            priority = 3    // High
        });
        createDefect.StatusCode.Should().Be(HttpStatusCode.OK);
        var defect = await createDefect.Content.ReadFromJsonAsync<JsonElement>(Json);
        var defectId = defect.GetProperty("id").GetGuid();
        defect.GetProperty("status").GetInt32().Should().Be(1); // New

        var assign = await admin.PostAsJsonAsync($"/api/v1/defects/{defectId}/status", new
        {
            id = defectId,
            targetStatus = 2, // Assigned
            assignToUserId = Guid.NewGuid()
        });
        assign.StatusCode.Should().Be(HttpStatusCode.OK);
        (await assign.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("status").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Registro_de_usuario_con_rol_desconocido_es_rechazado()
    {
        var admin = await AdminClientAsync();
        var register = await admin.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"x{Guid.NewGuid():N}@qaguardian.test",
            fullName = "Rol inválido",
            password = "Password.2026",
            roles = new[] { "RolQueNoExiste" }
        });
        register.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
