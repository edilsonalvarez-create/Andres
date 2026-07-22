using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QAGuardian.Infrastructure.Persistence;

/// <summary>
/// Fábrica de diseño para las herramientas de EF Core (<c>dotnet ef migrations</c> /
/// <c>dotnet ef database update</c>). Las migraciones se generan y validan contra SQL Server
/// (fuente de verdad de producción). Connection string:
/// <c>ConnectionStrings__DefaultConnection</c> o <c>QA_GUARDIAN_CONNECTION</c>.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<QAGuardianDbContext>
{
    public QAGuardianDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("QA_GUARDIAN_CONNECTION")
            ?? "Server=localhost,1433;Database=QAGuardian;User Id=sa;Password=Your_password123;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<QAGuardianDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new QAGuardianDbContext(options);
    }
}
