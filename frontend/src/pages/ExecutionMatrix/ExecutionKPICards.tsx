import { Box, Card, Stack, Typography, Skeleton, useTheme, useMediaQuery, Grid } from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ErrorIcon from "@mui/icons-material/Error";
import AssignmentIcon from "@mui/icons-material/Assignment";
import TrendingUpIcon from "@mui/icons-material/TrendingUp";

interface ExecutionKPICardsProps {
  totalTests: number;
  passedTests: number;
  failedTests: number;
  passRatePercent: number;
  isLoading?: boolean;
}

interface KPICardProps {
  icon: React.ReactNode;
  label: string;
  value: string | number;
  subtitle?: string;
  color: string;
  isLoading?: boolean;
}

function KPICard({
  icon,
  label,
  value,
  subtitle,
  color,
  isLoading,
}: KPICardProps) {
  return (
    <Card
      sx={{
        p: 2.5,
        background: "linear-gradient(135deg, #FFFFFF 0%, #F8FAFC 100%)",
        border: `1px solid #E5E7EB`,
        borderRadius: 2,
        transition: "all 200ms ease",
        position: "relative",
        overflow: "hidden",
        "&::before": {
          content: '""',
          position: "absolute",
          top: 0,
          left: 0,
          right: 0,
          height: 3,
          background: color,
        },
        "&:hover": {
          boxShadow: `0 8px 24px ${color}15`,
          transform: "translateY(-2px)",
        },
      }}
    >
      <Stack direction="row" spacing={2} alignItems="flex-start">
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            width: 44,
            height: 44,
            borderRadius: 1.5,
            background: `${color}15`,
            color: color,
            flexShrink: 0,
          }}
        >
          {icon}
        </Box>

        <Box sx={{ flex: 1, minWidth: 0 }}>
          <Typography
            sx={{
              fontSize: 12,
              fontWeight: 600,
              color: "#6B7280",
              textTransform: "uppercase",
              letterSpacing: 0.5,
              mb: 0.5,
            }}
          >
            {label}
          </Typography>

          {isLoading ? (
            <>
              <Skeleton width="60%" height={32} sx={{ mb: 0.5 }} />
              <Skeleton width="80%" height={14} />
            </>
          ) : (
            <>
              <Typography
                sx={{
                  fontSize: 28,
                  fontWeight: 700,
                  color: "#1F2937",
                  lineHeight: 1.2,
                  mb: 0.25,
                }}
              >
                {value}
              </Typography>
              {subtitle && (
                <Typography
                  sx={{
                    fontSize: 12,
                    color: "#9CA3AF",
                  }}
                >
                  {subtitle}
                </Typography>
              )}
            </>
          )}
        </Box>
      </Stack>
    </Card>
  );
}

export default function ExecutionKPICards({
  totalTests,
  passedTests,
  failedTests,
  passRatePercent,
  isLoading = false,
}: ExecutionKPICardsProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));
  const isTablet = useMediaQuery(theme.breakpoints.down("md"));

  return (
    <Box sx={{ mb: 4 }}>
      <Grid
        container
        spacing={2}
        columns={isMobile ? 1 : isTablet ? 2 : 4}
      >
        <Grid item xs={1}>
          <KPICard
            icon={<AssignmentIcon sx={{ fontSize: 20 }} />}
            label="Total de Pruebas"
            value={totalTests}
            subtitle="Casos ejecutados"
            color="#2563EB"
            isLoading={isLoading}
          />
        </Grid>

        <Grid item xs={1}>
          <KPICard
            icon={<CheckCircleIcon sx={{ fontSize: 20 }} />}
            label="Exitosas"
            value={passedTests}
            subtitle="Pruebas pasadas"
            color="#16A34A"
            isLoading={isLoading}
          />
        </Grid>

        <Grid item xs={1}>
          <KPICard
            icon={<ErrorIcon sx={{ fontSize: 20 }} />}
            label="Fallidas"
            value={failedTests}
            subtitle="Pruebas con error"
            color="#DC2626"
            isLoading={isLoading}
          />
        </Grid>

        <Grid item xs={1}>
          <KPICard
            icon={<TrendingUpIcon sx={{ fontSize: 20 }} />}
            label="% Éxito"
            value={`${passRatePercent.toFixed(1)}%`}
            subtitle="Tasa de éxito"
            color={
              passRatePercent >= 90
                ? "#16A34A"
                : passRatePercent >= 70
                ? "#F59E0B"
                : "#DC2626"
            }
            isLoading={isLoading}
          />
        </Grid>
      </Grid>
    </Box>
  );
}
