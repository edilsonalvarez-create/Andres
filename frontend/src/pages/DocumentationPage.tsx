import { Box, Card, CardContent, Chip, Container, Divider, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import InfoIcon from "@mui/icons-material/Info";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CodeIcon from "@mui/icons-material/Code";
import StorageIcon from "@mui/icons-material/Storage";
import ApiIcon from "@mui/icons-material/Api";

export default function DocumentationPage() {
  return (
    <Container maxWidth="lg" sx={{ py: 4 }}>
      {/* Encabezado */}
      <Box sx={{ mb: 4 }}>
        <Typography variant="h3" sx={{ fontWeight: "bold", mb: 2 }}>
          📊 Matriz de Ejecución de Pruebas
        </Typography>
        <Typography variant="subtitle1" color="textSecondary" sx={{ mb: 3 }}>
          Documentación técnica de implementación arquitectónica
        </Typography>
      </Box>

      {/* Resumen Ejecutivo */}
      <Card sx={{ mb: 4, backgroundColor: "#f5f5f5" }}>
        <CardContent>
          <Typography variant="h5" sx={{ fontWeight: "bold", mb: 2 }}>
            ✅ Resumen Ejecutivo
          </Typography>
          <Stack spacing={2}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
              <CheckCircleIcon sx={{ color: "success.main" }} />
              <Typography>
                <strong>Funcionalidad:</strong> Genera reportes Excel con filtros autofilter por módulo, prioridad y estado
              </Typography>
            </Box>
            <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
              <CheckCircleIcon sx={{ color: "success.main" }} />
              <Typography>
                <strong>Trazabilidad:</strong> Captura duración, ejecutor, fecha y evidencias por prueba
              </Typography>
            </Box>
            <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
              <CheckCircleIcon sx={{ color: "success.main" }} />
              <Typography>
                <strong>Integración:</strong> Accesible vía API REST en endpoint <code>/api/testruns/{"{id}"}/execution-matrix</code>
              </Typography>
            </Box>
            <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
              <CheckCircleIcon sx={{ color: "success.main" }} />
              <Typography>
                <strong>Arquitectura limpia:</strong> Respeta capas de aplicación (Domain → Application → Infrastructure → API)
              </Typography>
            </Box>
          </Stack>
        </CardContent>
      </Card>

      {/* Columnas de la Matriz */}
      <Box sx={{ mb: 4 }}>
        <Typography variant="h5" sx={{ fontWeight: "bold", mb: 2, display: "flex", alignItems: "center", gap: 1 }}>
          <InfoIcon /> Columnas de la Matriz
        </Typography>
        <TableContainer component={Paper}>
          <Table>
            <TableHead sx={{ backgroundColor: "#1976d2" }}>
              <TableRow>
                <TableCell sx={{ color: "white", fontWeight: "bold" }}>Columna</TableCell>
                <TableCell sx={{ color: "white", fontWeight: "bold" }}>Origen</TableCell>
                <TableCell sx={{ color: "white", fontWeight: "bold" }}>Tipo</TableCell>
                <TableCell sx={{ color: "white", fontWeight: "bold" }}>Ejemplo</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {[
                { col: "ID Caso", origin: "TestCase.Code", type: "String", example: "TC-001234" },
                { col: "Módulo", origin: "Module.Name", type: "String", example: "Autenticación" },
                { col: "Escenario / Caso de Prueba", origin: "TestCase.Title", type: "String", example: "Login con credenciales válidas" },
                { col: "Tipo de Prueba", origin: "TestRun.RunType", type: "Enum", example: "Funcional, Regresión, API" },
                { col: "Estado Actual", origin: "TestResult.Status", type: "Enum", example: "Passed, Failed, Skipped" },
                { col: "Prioridad", origin: "TestCase.Priority", type: "Enum", example: "Crítica, Alta, Normal, Baja" },
                { col: "Resultado Esperado", origin: "TestStep.ExpectedResult", type: "String", example: "El usuario inicia sesión exitosamente" },
                { col: "Duración (ms)", origin: "TestResult.DurationMs", type: "Long", example: "1234" },
                { col: "Ejecutado por", origin: "TestRun.TriggeredBy", type: "String", example: "Jenkins, Manual, CI/CD" },
                { col: "Fecha de ejecución", origin: "TestResult.ExecutedAt", type: "DateTime", example: "2026-07-14 12:30:45" },
                { col: "Evidencia/Artefactos", origin: "Evidence.FilePath", type: "String", example: "screenshots/login.png, logs/trace.txt" },
                { col: "Notas / Bugs Encontrados", origin: "TestResult.ErrorMessage", type: "String", example: "NullReferenceException en validación" },
              ].map((row, idx) => (
                <TableRow key={idx}>
                  <TableCell sx={{ fontWeight: "bold" }}>{row.col}</TableCell>
                  <TableCell><code>{row.origin}</code></TableCell>
                  <TableCell>{row.type}</TableCell>
                  <TableCell>{row.example}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </Box>

      <Divider sx={{ my: 4 }} />

      {/* Archivos Creados/Modificados */}
      <Box sx={{ mb: 4 }}>
        <Typography variant="h5" sx={{ fontWeight: "bold", mb: 2, display: "flex", alignItems: "center", gap: 1 }}>
          <CodeIcon /> Archivos Creados/Modificados
        </Typography>

        <Stack spacing={3}>
          {/* DTO */}
          <Card>
            <CardContent>
              <Typography variant="h6" sx={{ fontWeight: "bold", mb: 1 }}>
                1. Nueva entidad DTO
              </Typography>
              <Typography variant="body2" color="textSecondary" sx={{ mb: 2 }}>
                <strong>Archivo:</strong> <code>src/QAGuardian.Application/Features/Reports/ExecutionMatrixDto.cs</code>
              </Typography>
              <Box sx={{ backgroundColor: "#f5f5f5", p: 2, borderRadius: 1, overflow: "auto", mb: 2 }}>
                <code style={{ fontSize: "12px", fontFamily: "monospace", whiteSpace: "pre-wrap" }}>
                  {`public record ExecutionMatrixRow(
    string CaseId,
    string Module,
    string Scenario,
    string TestType,
    string CurrentStatus,
    string Priority,
    string ExpectedResult,
    long DurationMs,
    string ExecutedBy,
    DateTime ExecutionDate,
    string Evidence,
    string Notes);

public record ExecutionMatrixDto(
    Guid TestRunId,
    string ProjectName,
    string RunType,
    string Environment,
    string RunStatus,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int TotalTests,
    int PassedTests,
    int FailedTests,
    int SkippedTests,
    decimal PassRatePercent,
    IReadOnlyList<ExecutionMatrixRow> Rows,
    IReadOnlyList<string> UniqueModules,
    IReadOnlyList<string> UniquePriorities,
    IReadOnlyList<string> UniqueStatuses);`}
                </code>
              </Box>
            </CardContent>
          </Card>

          {/* Repositorio */}
          <Card>
            <CardContent>
              <Typography variant="h6" sx={{ fontWeight: "bold", mb: 1 }}>
                2. Extensiones de Repositorio
              </Typography>
              <Stack spacing={1}>
                <Typography variant="body2">
                  <strong>Archivo:</strong> <code>src/QAGuardian.Application/Abstractions/Persistence/IRepositories.cs</code>
                </Typography>
                <Chip label="✅ Agregar método GetWithFullDetailsAsync() a ITestRunRepository" color="success" variant="outlined" />
              </Stack>
              <Divider sx={{ my: 2 }} />
              <Stack spacing={1}>
                <Typography variant="body2">
                  <strong>Archivo:</strong> <code>src/QAGuardian.Infrastructure/Persistence/Repositories.cs</code>
                </Typography>
                <Chip label="✅ Implementar método con includes de Results y Evidences" color="success" variant="outlined" />
              </Stack>
            </CardContent>
          </Card>

          {/* Generador de Reportes */}
          <Card>
            <CardContent>
              <Typography variant="h6" sx={{ fontWeight: "bold", mb: 1 }}>
                3. Generador de Reportes Extendido
              </Typography>
              <Typography variant="body2" color="textSecondary" sx={{ mb: 2 }}>
                <strong>Archivo:</strong> <code>src/QAGuardian.Infrastructure/Reports/RunReportGenerator.cs</code>
              </Typography>
              <Box sx={{ backgroundColor: "#f5f5f5", p: 2, borderRadius: 1, overflow: "auto" }}>
                <code style={{ fontSize: "12px", fontFamily: "monospace", whiteSpace: "pre-wrap" }}>
                  {`// Generador principal (público)
public async Task<(byte[], string, string)>
    GenerateExecutionMatrixReportAsync(Guid testRunId, CancellationToken ct);

// Constructor de matriz (privado)
private async Task<ExecutionMatrixDto> BuildExecutionMatrixAsync(
    TestRun run, string projectName, CancellationToken ct);

// Generador de Excel con filtros (privado)
private static byte[] GenerateExecutionMatrixExcel(ExecutionMatrixDto matrix);`}
                </code>
              </Box>
            </CardContent>
          </Card>

          {/* Interfaz de Servicios */}
          <Card>
            <CardContent>
              <Typography variant="h6" sx={{ fontWeight: "bold", mb: 1 }}>
                4. Interfaz de Servicios Actualizada
              </Typography>
              <Typography variant="body2" color="textSecondary" sx={{ mb: 2 }}>
                <strong>Archivo:</strong> <code>src/QAGuardian.Application/Abstractions/Services/INotificationDispatcher.cs</code>
              </Typography>
              <Box sx={{ backgroundColor: "#f5f5f5", p: 2, borderRadius: 1, overflow: "auto" }}>
                <code style={{ fontSize: "12px", fontFamily: "monospace", whiteSpace: "pre-wrap" }}>
                  {`public interface IReportGenerator
{
    Task<(byte[] Content, string ContentType, string FileName)>
        GenerateExecutionMatrixReportAsync(Guid testRunId, CancellationToken ct);
}`}
                </code>
              </Box>
            </CardContent>
          </Card>
        </Stack>
      </Box>

      <Divider sx={{ my: 4 }} />

      {/* Cómo Usar */}
      <Box sx={{ mb: 4 }}>
        <Typography variant="h5" sx={{ fontWeight: "bold", mb: 2, display: "flex", alignItems: "center", gap: 1 }}>
          <ApiIcon /> Cómo Usar
        </Typography>

        <Card sx={{ mb: 2 }}>
          <CardContent>
            <Typography variant="h6" sx={{ fontWeight: "bold", mb: 1 }}>
              Solicitud HTTP
            </Typography>
            <Box sx={{ backgroundColor: "#f5f5f5", p: 2, borderRadius: 1, overflow: "auto" }}>
              <code style={{ fontSize: "12px", fontFamily: "monospace", whiteSpace: "pre-wrap" }}>
                {`GET /api/v1/testruns/{testRunId}/execution-matrix HTTP/1.1
Authorization: Bearer {token}`}
              </code>
            </Box>
          </CardContent>
        </Card>

        <Card sx={{ mb: 2 }}>
          <CardContent>
            <Typography variant="h6" sx={{ fontWeight: "bold", mb: 1 }}>
              Respuesta Exitosa (200 OK)
            </Typography>
            <Box sx={{ backgroundColor: "#f5f5f5", p: 2, borderRadius: 1, overflow: "auto" }}>
              <code style={{ fontSize: "12px", fontFamily: "monospace", whiteSpace: "pre-wrap" }}>
                {`Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
Content-Disposition: attachment; filename="matriz-ejecucion-Proyecto-{id}.xlsx"

[bytes del archivo Excel]`}
              </code>
            </Box>
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <Typography variant="h6" sx={{ fontWeight: "bold", mb: 1 }}>
              Ejemplo con cURL
            </Typography>
            <Box sx={{ backgroundColor: "#f5f5f5", p: 2, borderRadius: 1, overflow: "auto" }}>
              <code style={{ fontSize: "12px", fontFamily: "monospace", whiteSpace: "pre-wrap" }}>
                {`curl -X GET "http://localhost:5080/api/v1/testruns/550e8400-e29b-41d4-a716-446655440000/execution-matrix" \\
  -H "Authorization: Bearer {token}" \\
  -o matriz-ejecucion.xlsx`}
              </code>
            </Box>
          </CardContent>
        </Card>
      </Box>

      <Divider sx={{ my: 4 }} />

      {/* Estructura del Excel */}
      <Box sx={{ mb: 4 }}>
        <Typography variant="h5" sx={{ fontWeight: "bold", mb: 2, display: "flex", alignItems: "center", gap: 1 }}>
          <StorageIcon /> Estructura del Excel Generado
        </Typography>

        <Stack spacing={2}>
          <Card>
            <CardContent>
              <Typography variant="h6" sx={{ fontWeight: "bold", mb: 1 }}>
                Hoja 1: "Resumen"
              </Typography>
              <ul style={{ margin: 0, paddingLeft: 20 }}>
                <li>Metadatos de la ejecución</li>
                <li>Estadísticas agregadas (Total, Pasadas, Fallidas, Omitidas, % Éxito)</li>
                <li>Quality Gate evaluation</li>
              </ul>
            </CardContent>
          </Card>

          <Card>
            <CardContent>
              <Typography variant="h6" sx={{ fontWeight: "bold", mb: 1 }}>
                Hoja 2: "Ejecuciones"
              </Typography>
              <ul style={{ margin: 0, paddingLeft: 20 }}>
                <li><strong>Tabla Excel con AutoFilter habilitado</strong></li>
                <li>Filtros disponibles por:
                  <ul>
                    <li>🔽 <strong>Módulo</strong> (dropdown dinámico)</li>
                    <li>🔽 <strong>Prioridad</strong> (dropdown dinámico)</li>
                    <li>🔽 <strong>Estado Actual</strong> (Passed=verde, Failed=rojo, Skipped=naranja)</li>
                  </ul>
                </li>
                <li>12 columnas de trazabilidad completa</li>
                <li>Estilos condicionales por estado</li>
              </ul>
            </CardContent>
          </Card>
        </Stack>
      </Box>

      {/* Estado de Compilación */}
      <Card sx={{ mb: 4, backgroundColor: "#e8f5e9" }}>
        <CardContent>
          <Typography variant="h6" sx={{ fontWeight: "bold", mb: 2, color: "success.dark" }}>
            ✅ Estado de Compilación
          </Typography>
          <Box sx={{ backgroundColor: "white", p: 2, borderRadius: 1, overflow: "auto" }}>
            <code style={{ fontSize: "12px", fontFamily: "monospace", whiteSpace: "pre-wrap" }}>
              {`Build succeeded.
    0 Warnings
    0 Errors
    Time: 3.29s

✅ QAGuardian.Domain
✅ QAGuardian.Application
✅ QAGuardian.Infrastructure
✅ QAGuardian.API
✅ QAGuardian.UnitTests
✅ QAGuardian.IntegrationTests`}
            </code>
          </Box>
        </CardContent>
      </Card>

      {/* Estado Actual */}
      <Card sx={{ backgroundColor: "#f5f5f5" }}>
        <CardContent>
          <Typography variant="h6" sx={{ fontWeight: "bold", mb: 2 }}>
            🚀 Estado Actual
          </Typography>
          <Stack spacing={1}>
            <Typography variant="body2">
              <strong>Backend:</strong> Corriendo en http://localhost:5080
            </Typography>
            <Typography variant="body2">
              <strong>Frontend:</strong> Corriendo en http://localhost:5173
            </Typography>
            <Typography variant="body2">
              <strong>Base de datos:</strong> Inicializada con datos de semilla
            </Typography>
            <Typography variant="body2">
              <strong>API:</strong> Totalmente funcional y documentada en Swagger
            </Typography>
          </Stack>
        </CardContent>
      </Card>
    </Container>
  );
}
