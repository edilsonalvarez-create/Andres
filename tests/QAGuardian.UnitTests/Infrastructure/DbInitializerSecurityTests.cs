using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using QAGuardian.Infrastructure.Identity;
using QAGuardian.Infrastructure.Persistence;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

/// <summary>
/// Regresión Sprint 2 (OWASP A07:2025): defensa en profundidad — <see cref="DbInitializer"/>
/// rechaza sembrar el admin con la contraseña pública que documentaban versiones anteriores del
/// README fuera de Development, incluso si algún llamador se saltara la validación de
/// <c>Program.cs</c>.
/// </summary>
public class DbInitializerSecurityTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly QAGuardianDbContext _context;

    public DbInitializerSecurityTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<QAGuardianDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new QAGuardianDbContext(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task Rechaza_la_contrasena_publica_conocida_fuera_de_Development()
    {
        var initializer = CreateInitializer(Environments.Production);

        var act = () => initializer.InitializeAsync("admin@qaguardian.local", "QaGuardian.2026!");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*públicamente conocido*");
    }

    [Fact]
    public async Task Acepta_la_contrasena_publica_conocida_en_Development()
    {
        var initializer = CreateInitializer(Environments.Development);

        await initializer.InitializeAsync("admin@qaguardian.local", "QaGuardian.2026!");

        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == "admin@qaguardian.local");
        admin.Should().NotBeNull();
    }

    [Fact]
    public async Task Acepta_una_contrasena_unica_y_crea_el_admin()
    {
        var initializer = CreateInitializer(Environments.Production);

        await initializer.InitializeAsync("admin@qaguardian.local", "Otra.Contrasena.Segura.2026!");

        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == "admin@qaguardian.local");
        admin.Should().NotBeNull();
    }

    [Fact]
    public async Task No_duplica_el_admin_si_ya_existe()
    {
        var initializer = CreateInitializer(Environments.Production);

        await initializer.InitializeAsync("admin@qaguardian.local", "Otra.Contrasena.Segura.2026!");
        await initializer.InitializeAsync("admin@qaguardian.local", "Otra.Contrasena.Segura.2026!");

        var count = await _context.Users.CountAsync(u => u.Email == "admin@qaguardian.local");
        count.Should().Be(1);
    }

    private DbInitializer CreateInitializer(string environmentName) =>
        new(_context, new BcryptPasswordHasher(), new TestHostEnvironment(environmentName),
            NullLogger<DbInitializer>.Instance);

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "QAGuardian.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
