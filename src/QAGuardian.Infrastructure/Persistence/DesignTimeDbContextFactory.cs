using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QAGuardian.Infrastructure.Persistence;

/// <summary>
/// Fábrica de diseño para las herramientas de EF Core (dotnet ef migrations …).
/// Las migraciones se generan para el proveedor SQL Server (el de producción);
/// no se conecta a ninguna base al generar.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<QAGuardianDbContext>
{
    public QAGuardianDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<QAGuardianDbContext>()
            .UseSqlServer("Server=localhost;Database=QAGuardian;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new QAGuardianDbContext(options);
    }
}
