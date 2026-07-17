import {
  Box,
  Autocomplete,
  TextField,
  Stack,
  useTheme,
  useMediaQuery,
} from "@mui/material";

interface ExecutionFiltersProps {
  modules: string[];
  priorities: string[];
  statuses: string[];
  moduleFilter: string;
  priorityFilter: string;
  statusFilter: string;
  onModuleChange: (value: string | null) => void;
  onPriorityChange: (value: string | null) => void;
  onStatusChange: (value: string | null) => void;
}

const priorityColors: Record<string, string> = {
  Critical: "#DC2626",
  High: "#F59E0B",
  Medium: "#2563EB",
  Low: "#6B7280",
};

const statusColors: Record<string, string> = {
  Passed: "#16A34A",
  Failed: "#DC2626",
  Skipped: "#F59E0B",
  Running: "#2563EB",
  Blocked: "#6B7280",
};

export default function ExecutionFilters({
  modules,
  priorities,
  statuses,
  moduleFilter,
  priorityFilter,
  statusFilter,
  onModuleChange,
  onPriorityChange,
  onStatusChange,
}: ExecutionFiltersProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  const filterConfig = [
    {
      label: "Módulo",
      value: moduleFilter,
      options: modules,
      onChange: onModuleChange,
    },
    {
      label: "Prioridad",
      value: priorityFilter,
      options: priorities,
      onChange: onPriorityChange,
      colors: priorityColors,
    },
    {
      label: "Estado",
      value: statusFilter,
      options: statuses,
      onChange: onStatusChange,
      colors: statusColors,
    },
  ];

  return (
    <Box sx={{ mb: 3 }}>
      <Stack
        direction={isMobile ? "column" : "row"}
        spacing={1.5}
        sx={{
          alignItems: isMobile ? "stretch" : "center",
          flexWrap: "wrap",
        }}
      >
        {filterConfig.map((filter) => (
          <Autocomplete
            key={filter.label}
            options={filter.options}
            value={filter.value || null}
            onChange={(_, value) => filter.onChange(value)}
            clearOnBlur
            isOptionEqualToValue={(option, value) => option === value}
            renderInput={(params) => (
              <TextField
                {...params}
                label={filter.label}
                size="small"
                placeholder={`Seleccionar ${filter.label.toLowerCase()}...`}
                sx={{
                  minWidth: isMobile ? "100%" : 180,
                  "& .MuiOutlinedInput-root": {
                    backgroundColor: "#FFFFFF",
                    borderRadius: 1,
                    border: "1px solid #E5E7EB",
                    transition: "all 200ms ease",
                    "&:hover": {
                      borderColor: "#2563EB",
                    },
                    "&.Mui-focused": {
                      borderColor: "#2563EB",
                      boxShadow: "0 0 0 3px rgba(37, 99, 235, 0.1)",
                    },
                  },
                  "& .MuiOutlinedInput-input": {
                    padding: "10px 12px",
                    fontSize: 13,
                  },
                }}
              />
            )}
            renderOption={(props, option) => (
              <Box
                component="li"
                sx={{
                  py: 1,
                  px: 1.5,
                  fontSize: 13,
                  display: "flex",
                  alignItems: "center",
                  gap: 1,
                  "&:hover": {
                    backgroundColor: "#F0F9FF",
                  },
                }}
                {...props}
              >
                {filter.colors && (
                  <Box
                    sx={{
                      width: 8,
                      height: 8,
                      borderRadius: "50%",
                      backgroundColor: filter.colors[option] || "#9CA3AF",
                      flexShrink: 0,
                    }}
                  />
                )}
                {option}
              </Box>
            )}
            noOptionsText="Sin opciones"
          />
        ))}
      </Stack>
    </Box>
  );
}
