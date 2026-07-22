using QAGuardian.Domain.Enums;

namespace QAGuardian.Application.Abstractions.Services;

/// <summary>Mensaje de notificación multi-canal.</summary>
public record NotificationMessage(
    NotificationEvents Event,
    string Title,
    string Body,
    Guid? ProjectId,
    string? LinkUrl);

/// <summary>Puerto: despacha notificaciones a todos los canales configurados que escuchan el evento.</summary>
public interface INotificationDispatcher
{
    Task DispatchAsync(NotificationMessage message, CancellationToken ct = default);
}

/// <summary>Puerto: notifica progreso de ejecuciones en tiempo real (SignalR).</summary>
public interface IRunProgressNotifier
{
    Task RunStatusChangedAsync(Guid testRunId, string status, CancellationToken ct = default);
    Task RunCompletedAsync(Guid testRunId, int passed, int failed, string gateStatus, CancellationToken ct = default);
}

/// <summary>Puerto: encola trabajos en segundo plano (Hangfire).</summary>
public interface IBackgroundJobScheduler
{
    string EnqueueTestRunExecution(Guid testRunId);
    /// <summary>
    /// Encola validación de esquema. Args inspectables: runId + projectId + nombres de entorno.
    /// Nunca connection strings (Sprint 12).
    /// </summary>
    string EnqueueDatabaseValidation(
        Guid validationRunId, Guid projectId, string sourceEnvironment, string targetEnvironment);
}

/// <summary>Puerto: almacenamiento de evidencias y reportes.</summary>
public interface IEvidenceStorage
{
    Task<string> SaveAsync(string relativePath, Stream content, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string path, CancellationToken ct = default);
    string GetAbsolutePath(string relativePath);
    string CreateRunDirectory(Guid testRunId);
}

/// <summary>Formatos de reporte soportados.</summary>
public enum ReportFormat { Pdf, Excel, Html, Json, Csv, Word, Xml, PowerPoint }

/// <summary>Puerto: generación de reportes de ejecución y de dashboard en múltiples formatos.</summary>
public interface IReportGenerator
{
    Task<(byte[] Content, string ContentType, string FileName)> GenerateRunReportAsync(
        Guid testRunId, ReportFormat format, CancellationToken ct = default);

    /// <summary>Genera un reporte del dashboard ejecutivo (PDF o Excel).</summary>
    (byte[] Content, string ContentType, string FileName) GenerateDashboardReport(
        Features.Dashboard.DashboardDto stats, string title, ReportFormat format);

    /// <summary>Obtiene la matriz de ejecución en DTO para visualización interactiva.</summary>
    Task<QAGuardian.Application.Features.Reports.ExecutionMatrixDto> GetExecutionMatrixAsync(
        Guid testRunId, CancellationToken ct = default);

    /// <summary>Genera matriz de ejecución con filtros en Excel para un TestRun específico.</summary>
    Task<(byte[] Content, string ContentType, string FileName)> GenerateExecutionMatrixReportAsync(
        Guid testRunId, CancellationToken ct = default);
}
