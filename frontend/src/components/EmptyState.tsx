import { Box, Button, CircularProgress, Typography } from "@mui/material";
import RefreshIcon from "@mui/icons-material/Refresh";
import FolderOffIcon from "@mui/icons-material/FolderOff";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import TouchAppIcon from "@mui/icons-material/TouchApp";

type Variant = "loading" | "no-data" | "no-selection" | "error";

interface Props {
  variant: Variant;
  title?: string;
  description?: string;
  onRetry?: () => void;
}

const DEFAULTS: Record<Variant, { icon: React.ReactNode; title: string; description: string }> = {
  loading: {
    icon: <CircularProgress size={40} />,
    title: "Cargando…",
    description: "Un momento, por favor.",
  },
  "no-selection": {
    icon: <TouchAppIcon sx={{ fontSize: 48 }} color="action" />,
    title: "Seleccione un proyecto",
    description: "Elija un proyecto arriba para ver su información.",
  },
  "no-data": {
    icon: <FolderOffIcon sx={{ fontSize: 48 }} color="action" />,
    title: "Sin resultados",
    description: "Todavía no hay registros para mostrar aquí.",
  },
  error: {
    icon: <ErrorOutlineIcon sx={{ fontSize: 48 }} color="error" />,
    title: "No se pudo cargar la información",
    description: "Ocurrió un problema de conexión. Intente nuevamente.",
  },
};

/**
 * Estado visual consistente para carga / sin selección / sin datos / error-con-reintento.
 * Reemplaza los callejones sin salida (spinner o Alert sin acción) repartidos por la app —
 * ver UX-UI-Audit.md hallazgos DB-03, CR-05.
 */
export default function EmptyState({ variant, title, description, onRetry }: Props) {
  const d = DEFAULTS[variant];
  return (
    <Box
      className="flex flex-col items-center justify-center gap-3 py-16 text-center"
      role={variant === "error" ? "alert" : "status"}
      aria-live={variant === "error" ? "assertive" : "polite"}
    >
      {d.icon}
      <Typography variant="h6" fontWeight={600}>
        {title ?? d.title}
      </Typography>
      <Typography variant="body2" color="text.secondary" className="max-w-sm">
        {description ?? d.description}
      </Typography>
      {variant === "error" && onRetry && (
        <Button variant="outlined" startIcon={<RefreshIcon />} onClick={onRetry} sx={{ mt: 1 }}>
          Reintentar
        </Button>
      )}
    </Box>
  );
}
