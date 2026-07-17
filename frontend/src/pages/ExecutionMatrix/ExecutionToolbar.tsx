import {
  Box,
  TextField,
  Button,
  InputAdornment,
  useTheme,
  useMediaQuery,
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import ClearIcon from "@mui/icons-material/Clear";

interface ExecutionToolbarProps {
  searchValue: string;
  onSearchChange: (value: string) => void;
  onClearFilters: () => void;
  hasActiveFilters: boolean;
}

export default function ExecutionToolbar({
  searchValue,
  onSearchChange,
  onClearFilters,
  hasActiveFilters,
}: ExecutionToolbarProps) {
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  return (
    <Box
      sx={{
        mb: 3,
        display: "flex",
        flexDirection: isMobile ? "column" : "row",
        gap: 2,
        alignItems: isMobile ? "stretch" : "center",
      }}
    >
      <TextField
        placeholder="Buscar por ID, escenario, usuario..."
        value={searchValue}
        onChange={(e) => onSearchChange(e.target.value)}
        size="small"
        sx={{
          flex: 1,
          "& .MuiOutlinedInput-root": {
            backgroundColor: "#FFFFFF",
            borderRadius: 1,
            border: "1px solid #E5E7EB",
            fontSize: 13,
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
            "&::placeholder": {
              color: "#9CA3AF",
              opacity: 1,
            },
          },
        }}
        InputProps={{
          startAdornment: (
            <InputAdornment position="start">
              <SearchIcon sx={{ color: "#9CA3AF", fontSize: 18 }} />
            </InputAdornment>
          ),
        }}
      />

      <Button
        variant="outlined"
        size="small"
        startIcon={<ClearIcon />}
        onClick={onClearFilters}
        disabled={!hasActiveFilters}
        sx={{
          textTransform: "none",
          fontWeight: 600,
          fontSize: 13,
          borderColor: "#E5E7EB",
          color: "#6B7280",
          transition: "all 200ms ease",
          "&:hover": {
            borderColor: "#F59E0B",
            backgroundColor: "#FFFBEB",
            color: "#D97706",
          },
          "&.Mui-disabled": {
            borderColor: "#E5E7EB",
            color: "#D1D5DB",
          },
        }}
      >
        Limpiar
      </Button>
    </Box>
  );
}
