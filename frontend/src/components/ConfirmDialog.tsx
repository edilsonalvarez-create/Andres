import {
  Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle,
} from "@mui/material";
import WarningAmberIcon from "@mui/icons-material/WarningAmber";

interface Props {
  open: boolean;
  title: string;
  description: string;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * Reemplaza `window.confirm` (hallazgo CR-04 del UX-UI-Audit): diálogo consistente con el
 * resto de la aplicación, con foco gestionado por MUI y opción de estilo destructivo para
 * acciones irreversibles.
 */
export default function ConfirmDialog({
  open, title, description, confirmLabel = "Confirmar", cancelLabel = "Cancelar",
  destructive = false, onConfirm, onCancel,
}: Props) {
  return (
    <Dialog open={open} onClose={onCancel} maxWidth="xs" fullWidth>
      <DialogTitle className="flex items-center gap-2">
        {destructive && <WarningAmberIcon color="warning" />}
        {title}
      </DialogTitle>
      <DialogContent>
        <DialogContentText>{description}</DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} autoFocus>{cancelLabel}</Button>
        <Button onClick={onConfirm} color={destructive ? "error" : "primary"} variant="contained">
          {confirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
