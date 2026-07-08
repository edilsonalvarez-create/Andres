export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresInMinutes: number;
  userId: string;
  email: string;
  fullName: string;
  roles: string[];
}

export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface Project {
  id: string;
  code: string;
  name: string;
  description?: string;
  repositoryUrl?: string;
  isActive: boolean;
  qualityGateId?: string;
  modulesCount: number;
  createdAt: string;
}

export interface TestStep {
  order: number;
  action: string;
  expectedResult: string;
}

export interface TestCase {
  id: string;
  projectId: string;
  code: string;
  title: string;
  preconditions?: string;
  type: number;
  priority: number;
  status: number;
  framework: number;
  automationScriptPath?: string;
  tags?: string;
  steps: TestStep[];
}

export interface TestRun {
  id: string;
  projectId: string;
  runType: number;
  environment: number;
  status: number;
  triggeredBy: string;
  commitSha?: string;
  pullRequestNumber?: number;
  startedAt?: string;
  completedAt?: string;
  durationSeconds?: number;
  totalTests: number;
  passed: number;
  failed: number;
  skipped: number;
  passRatePercent: number;
  gateStatus?: string;
  deploymentApproved?: boolean;
  errorMessage?: string;
}

export interface TestResult {
  id: string;
  name: string;
  status: number;
  durationMs: number;
  errorMessage?: string;
  stackTrace?: string;
  executedAt: string;
  evidences: { id: string; type: number; filePath: string; contentType: string; sizeBytes: number }[];
}

export interface Defect {
  id: string;
  projectId: string;
  code: string;
  title: string;
  description: string;
  severity: number;
  priority: number;
  status: number;
  sprint?: string;
  version?: string;
  createdAt: string;
}

export interface DashboardStats {
  totalProjects: number;
  totalTestCases: number;
  automatedTestCases: number;
  automationCoveragePercent: number;
  runsLast30Days: number;
  testsExecuted: number;
  testsPassed: number;
  testsFailed: number;
  testsPending: number;
  passRatePercent: number;
  avgRunDurationSeconds: number;
  openDefects: number;
  criticalDefectsOpen: number;
  vulnerabilitiesHighOrCritical: number;
  qualityScore: number;
  errorsByModule: { moduleName: string; failedCount: number }[];
  trend: { date: string; passed: number; failed: number; passRate: number }[];
}

export const TEST_TYPES: Record<number, string> = {
  1: "Funcional", 2: "Regresión", 3: "API", 4: "Rendimiento", 5: "Seguridad",
  6: "Visual", 7: "Base de datos", 8: "Smoke", 9: "Unitaria", 10: "Integración",
  11: "End to End", 12: "Componente",
};

export const PRIORITIES: Record<number, string> = { 1: "Baja", 2: "Media", 3: "Alta", 4: "Crítica" };
export const ENVIRONMENTS: Record<number, string> = { 1: "Development", 2: "QA", 3: "Staging", 4: "Production" };
export const RUN_STATUS: Record<number, string> = { 1: "Pendiente", 2: "En curso", 3: "Completada", 4: "Fallida", 5: "Cancelada" };
export const RESULT_STATUS: Record<number, string> = { 1: "Exitosa", 2: "Fallida", 3: "Omitida", 4: "Bloqueada", 5: "Inestable" };
export const DEFECT_STATUS: Record<number, string> = {
  1: "Nuevo", 2: "Asignado", 3: "En progreso", 4: "Resuelto", 5: "Verificado", 6: "Cerrado", 7: "Reabierto", 8: "Rechazado",
};
export const SEVERITIES: Record<number, string> = { 1: "Trivial", 2: "Menor", 3: "Mayor", 4: "Crítica", 5: "Bloqueante" };
export const FRAMEWORKS: Record<number, string> = { 0: "Manual", 1: "Playwright", 2: "Postman", 3: "JMeter", 4: "OWASP ZAP", 5: "SQL" };
