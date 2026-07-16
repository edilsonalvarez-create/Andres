import { lazy, Suspense } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { Box, CircularProgress } from "@mui/material";
import { useAuth } from "./auth/AuthContext";
import Layout from "./components/Layout";
import LoginPage from "./pages/LoginPage";

const DashboardPage = lazy(() => import("./pages/DashboardPage"));
const ProjectsPage = lazy(() => import("./pages/ProjectsPage"));
const CatalogPage = lazy(() => import("./pages/CatalogPage"));
const TestCasesPage = lazy(() => import("./pages/TestCasesPage"));
const TestRunsPage = lazy(() => import("./pages/TestRunsPage"));
const DefectsPage = lazy(() => import("./pages/DefectsPage"));
const IntegrationsPage = lazy(() => import("./pages/IntegrationsPage"));
const UsersPage = lazy(() => import("./pages/UsersPage"));
const DocumentationPage = lazy(() => import("./pages/DocumentationPage"));
const QualityGatesPage = lazy(() => import("./pages/QualityGatesPage"));
const TraceabilityPage = lazy(() => import("./pages/TraceabilityPage"));
const NotificationsAdminPage = lazy(() => import("./pages/NotificationsAdminPage"));
const ApprovalsPage = lazy(() => import("./pages/ApprovalsPage"));
const AuditPage = lazy(() => import("./pages/AuditPage"));

function PageLoader() {
  return (
    <Box className="flex items-center justify-center h-screen">
      <CircularProgress />
    </Box>
  );
}

function LazyPage({ children }: { children: React.ReactElement }) {
  return <Suspense fallback={<PageLoader />}>{children}</Suspense>;
}

function RequireAuth({ children }: { children: React.ReactElement }) {
  const { auth, loading } = useAuth();
  // Mientras se intenta renovar la sesión en silencio (cookie httpOnly), no redirigir todavía:
  // evita un "flash" al login en cada recarga de página.
  if (loading) {
    return <PageLoader />;
  }
  return auth ? children : <Navigate to="/login" replace />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        path="/"
        element={
          <RequireAuth>
            <Layout />
          </RequireAuth>
        }
      >
        <Route index element={<LazyPage><DashboardPage /></LazyPage>} />
        <Route path="proyectos" element={<LazyPage><ProjectsPage /></LazyPage>} />
        <Route path="catalogo" element={<LazyPage><CatalogPage /></LazyPage>} />
        <Route path="casos" element={<LazyPage><TestCasesPage /></LazyPage>} />
        <Route path="ejecuciones" element={<LazyPage><TestRunsPage /></LazyPage>} />
        {/* La Matriz se fusionó dentro de Ejecuciones (maestro→detalle). */}
        <Route path="matriz-ejecucion" element={<Navigate to="/ejecuciones" replace />} />
        <Route path="defectos" element={<LazyPage><DefectsPage /></LazyPage>} />
        <Route path="quality-gates" element={<LazyPage><QualityGatesPage /></LazyPage>} />
        <Route path="trazabilidad" element={<LazyPage><TraceabilityPage /></LazyPage>} />
        <Route path="aprobaciones" element={<LazyPage><ApprovalsPage /></LazyPage>} />
        <Route path="notificaciones" element={<LazyPage><NotificationsAdminPage /></LazyPage>} />
        <Route path="auditoria" element={<LazyPage><AuditPage /></LazyPage>} />
        <Route path="integraciones" element={<LazyPage><IntegrationsPage /></LazyPage>} />
        <Route path="usuarios" element={<LazyPage><UsersPage /></LazyPage>} />
        <Route path="documentacion" element={<LazyPage><DocumentationPage /></LazyPage>} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
