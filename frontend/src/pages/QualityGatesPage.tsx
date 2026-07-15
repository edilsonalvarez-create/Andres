import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Card, CardContent, Chip, CircularProgress, Dialog, DialogActions,
  DialogContent, DialogTitle, Grid2 as Grid, IconButton, MenuItem, Snackbar, Stack, TextField, Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import DeleteIcon from "@mui/icons-material/Delete";
import EditIcon from "@mui/icons-material/Edit";
import { api } from "../api/client";
import AuditTrailComponent from "../components/AuditTrailComponent";

// Coincide con QAGuardian.Domain.Enums.GateMetric (serializado como número).
const GATE_METRICS: Record<number, string> = {
  1: "Pass Rate (%)",
  2: "Coverage (%)",
  3: "Vulnerabilidades Críticas",
  4: "Vulnerabilidades Altas",
  5: "Bugs",
  6: "Code Smells",
  7: "Duplicación (%)",
  8: "Security Hotspots",
  9: "Tiempo de Respuesta Promedio (ms)",
  10: "Tasa de Error (%)",
  11: "Defectos Críticos Abiertos",
};

// Coincide con QAGuardian.Domain.Enums.GateOperator (serializado como número).
const GATE_OPERATORS: Record<number, string> = {
  1: ">=",
  2: "<=",
  3: "=",
};

interface GateCondition {
  id?: string;
  metric: number;
  operator: number;
  threshold: number;
  isBlocking: boolean;
}

interface QualityGate {
  id: string;
  name: string;
  isDefault: boolean;
  conditions: GateCondition[];
}

const emptyCondition = (): GateCondition => ({
  metric: 1,
  operator: 1,
  threshold: 80,
  isBlocking: true,
});

export default function QualityGatesPage() {
  const [gates, setGates] = useState<QualityGate[]>([]);
  const [loading, setLoading] = useState(true);
  const [toast, setToast] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);

  const [form, setForm] = useState({
    name: "",
    conditions: [] as GateCondition[],
  });

  useEffect(() => {
    loadGates();
  }, []);

  const loadGates = async () => {
    setLoading(true);
    try {
      const { data } = await api.get<QualityGate[]>("/qualitygates");
      setGates(data);
    } catch (error) {
      console.error("Error loading gates:", error);
      setToast("Error cargando quality gates");
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = async () => {
    if (!form.name.trim()) {
      setToast("El nombre es requerido");
      return;
    }
    if (form.conditions.length === 0) {
      setToast("Debe agregar al menos una condición");
      return;
    }

    try {
      await api.post("/qualitygates", {
        name: form.name,
        isDefault: false,
        conditions: form.conditions.map((c) => ({
          metric: c.metric,
          operator: c.operator,
          threshold: c.threshold,
          isBlocking: c.isBlocking,
        })),
      });
      setToast("Quality Gate creado exitosamente");
      setCreateOpen(false);
      setForm({ name: "", conditions: [] });
      await loadGates();
    } catch (error) {
      console.error("Error creating gate:", error);
      setToast("Error creando quality gate");
    }
  };

  const handleUpdate = async () => {
    if (!form.name.trim()) {
      setToast("El nombre es requerido");
      return;
    }
    if (!editingId) return;

    try {
      await api.put(`/qualitygates/${editingId}`, {
        id: editingId,
        name: form.name,
        conditions: form.conditions.map((c) => ({
          metric: c.metric,
          operator: c.operator,
          threshold: c.threshold,
          isBlocking: c.isBlocking,
        })),
      });
      setToast("Quality Gate actualizado exitosamente");
      setEditOpen(false);
      setEditingId(null);
      setForm({ name: "", conditions: [] });
      await loadGates();
    } catch (error) {
      console.error("Error updating gate:", error);
      setToast("Error actualizando quality gate");
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm("¿Estás seguro que quieres eliminar este gate?")) return;
    try {
      await api.delete(`/qualitygates/${id}`);
      setToast("Quality Gate eliminado");
      await loadGates();
    } catch (error) {
      console.error("Error deleting gate:", error);
      setToast("Error eliminando quality gate");
    }
  };

  const openEditDialog = (gate: QualityGate) => {
    setEditingId(gate.id);
    setForm({
      name: gate.name,
      conditions: gate.conditions.map((c) => ({ ...c, id: undefined })),
    });
    setEditOpen(true);
  };

  const addCondition = () => {
    setForm({ ...form, conditions: [...form.conditions, emptyCondition()] });
  };

  const removeCondition = (index: number) => {
    setForm({
      ...form,
      conditions: form.conditions.filter((_, i) => i !== index),
    });
  };

  const updateCondition = (index: number, field: keyof GateCondition, value: any) => {
    const newConditions = [...form.conditions];
    newConditions[index] = { ...newConditions[index], [field]: value };
    setForm({ ...form, conditions: newConditions });
  };

  const handleCloseDialog = () => {
    setCreateOpen(false);
    setEditOpen(false);
    setEditingId(null);
    setForm({ name: "", conditions: [] });
  };

  if (loading) {
    return (
      <Box className="flex items-center justify-center h-screen">
        <CircularProgress />
      </Box>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <Box className="flex items-center justify-between">
        <Typography variant="h5" fontWeight={700}>Quality Gates</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
          Crear Gate
        </Button>
      </Box>

      {gates.length === 0 && !loading && (
        <Alert severity="info">No hay quality gates configurados. Crea uno para empezar.</Alert>
      )}

      <Grid container spacing={2}>
        {gates.map((gate) => (
          <Grid key={gate.id} size={{ xs: 12, md: 6 }}>
            <Card>
              <CardContent>
                <Box className="flex justify-between items-start mb-4">
                  <div className="flex-1">
                    <Box className="flex items-center gap-2 mb-2">
                      <Typography variant="h6" fontWeight={700}>{gate.name}</Typography>
                      {gate.isDefault && <Chip label="Default" size="small" color="primary" />}
                    </Box>
                  </div>
                  <Box className="flex gap-1">
                    <IconButton size="small" onClick={() => openEditDialog(gate)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                    <IconButton size="small" onClick={() => handleDelete(gate.id)}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Box>
                </Box>

                <Stack spacing={1}>
                  <Typography variant="subtitle2" fontWeight={600}>Condiciones:</Typography>
                  {gate.conditions.map((cond, idx) => (
                    <Box key={idx} className="flex items-center gap-2 p-2 bg-gray-50 rounded">
                      <Chip size="small" label={GATE_METRICS[cond.metric] ?? cond.metric} variant="outlined" />
                      <span className="text-sm font-mono">{GATE_OPERATORS[cond.operator] ?? cond.operator}</span>
                      <Chip size="small" label={cond.threshold.toString()} />
                      {cond.isBlocking && <Chip size="small" label="Bloqueante" color="error" variant="outlined" />}
                    </Box>
                  ))}
                </Stack>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      {/* Create/Edit Dialog */}
      <Dialog open={createOpen || editOpen} onClose={handleCloseDialog} maxWidth="sm" fullWidth>
        <DialogTitle>{editingId ? "Editar Quality Gate" : "Crear Quality Gate"}</DialogTitle>
        <DialogContent className="flex flex-col gap-4 mt-4">
          <TextField
            label="Nombre"
            value={form.name}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            fullWidth
            placeholder="Ej: Production Release Gate"
          />

          <Box>
            <Typography variant="subtitle2" fontWeight={600} gutterBottom>Condiciones</Typography>
            {form.conditions.length === 0 && (
              <Alert severity="info" sx={{ mb: 2 }}>No hay condiciones. Agrega una para continuar.</Alert>
            )}
            {form.conditions.map((cond, idx) => (
              <Stack key={idx} direction="row" spacing={1} alignItems="center" sx={{ mb: 2 }}>
                <TextField
                  select
                  label="Métrica"
                  value={cond.metric}
                  onChange={(e) => updateCondition(idx, "metric", Number(e.target.value))}
                  size="small"
                  sx={{ width: 180 }}
                >
                  {Object.entries(GATE_METRICS).map(([value, label]) => (
                    <MenuItem key={value} value={Number(value)}>{label}</MenuItem>
                  ))}
                </TextField>
                <TextField
                  select
                  label="Operador"
                  value={cond.operator}
                  onChange={(e) => updateCondition(idx, "operator", Number(e.target.value))}
                  size="small"
                  sx={{ width: 70 }}
                >
                  {Object.entries(GATE_OPERATORS).map(([value, label]) => (
                    <MenuItem key={value} value={Number(value)}>{label}</MenuItem>
                  ))}
                </TextField>
                <TextField
                  type="number"
                  label="Umbral"
                  value={cond.threshold}
                  onChange={(e) => updateCondition(idx, "threshold", parseFloat(e.target.value))}
                  size="small"
                  sx={{ width: 100 }}
                />
                <Chip
                  label={cond.isBlocking ? "Bloqueante" : "Advertencia"}
                  onClick={() => updateCondition(idx, "isBlocking", !cond.isBlocking)}
                  color={cond.isBlocking ? "error" : "default"}
                  variant="outlined"
                />
                <IconButton size="small" onClick={() => removeCondition(idx)}>
                  <DeleteIcon fontSize="small" />
                </IconButton>
              </Stack>
            ))}
            <Button size="small" onClick={addCondition} sx={{ mt: 1 }}>
              + Agregar Condición
            </Button>
          </Box>

          {editingId && (
            <Box sx={{ mt: 2 }}>
              <AuditTrailComponent pathContains={editingId} maxRows={5} />
            </Box>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={handleCloseDialog}>Cancelar</Button>
          <Button
            variant="contained"
            onClick={editingId ? handleUpdate : handleCreate}
          >
            {editingId ? "Actualizar" : "Crear"}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Toast */}
      <Snackbar
        open={!!toast}
        autoHideDuration={4000}
        onClose={() => setToast(null)}
        message={toast}
      />
    </div>
  );
}
