import { useState, type FormEvent } from "react";
import {
  Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField,
} from "@mui/material";
import { api } from "../api/client";

export default function ChangePasswordDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState(false);

  const reset = () => { setCurrent(""); setNext(""); setConfirm(""); setError(null); setOk(false); };
  const close = () => { reset(); onClose(); };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    if (next !== confirm) { setError("La confirmación no coincide con la nueva contraseña."); return; }
    try {
      await api.post("/auth/change-password", { currentPassword: current, newPassword: next });
      setOk(true);
      setCurrent(""); setNext(""); setConfirm("");
    } catch (err: unknown) {
      const detail = (err as { response?: { data?: { error?: string } } }).response?.data?.error;
      setError(detail ?? "No fue posible cambiar la contraseña.");
    }
  };

  return (
    <Dialog open={open} onClose={close} maxWidth="xs" fullWidth>
      <form onSubmit={submit}>
        <DialogTitle>Cambiar contraseña</DialogTitle>
        <DialogContent className="flex flex-col gap-3 pt-2">
          {ok && <Alert severity="success">Contraseña actualizada correctamente.</Alert>}
          {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}
          <TextField label="Contraseña actual" type="password" required
            value={current} onChange={(e) => setCurrent(e.target.value)} />
          <TextField label="Nueva contraseña" type="password" required
            value={next} onChange={(e) => setNext(e.target.value)}
            helperText="Mínimo 10 caracteres, con mayúscula, minúscula y dígito." />
          <TextField label="Confirmar nueva contraseña" type="password" required
            value={confirm} onChange={(e) => setConfirm(e.target.value)} />
        </DialogContent>
        <DialogActions>
          <Button onClick={close}>{ok ? "Cerrar" : "Cancelar"}</Button>
          <Button type="submit" variant="contained" disabled={ok}>Guardar</Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
