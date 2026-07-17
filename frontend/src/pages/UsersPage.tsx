import { useEffect, useState, type FormEvent } from "react";
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle,
  IconButton, MenuItem, Paper, Select, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, TextField, Tooltip, Typography, type SelectChangeEvent,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import BadgeIcon from "@mui/icons-material/Badge";
import VpnKeyIcon from "@mui/icons-material/VpnKey";
import AutorenewIcon from "@mui/icons-material/Autorenew";
import BlockIcon from "@mui/icons-material/Block";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import { api } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import type { Paged } from "../types";

// Genera una contraseña fuerte que cumple la política (10+, mayúscula, minúscula, dígito, símbolo).
function generatePassword(): string {
  const U = "ABCDEFGHJKLMNPQRSTUVWXYZ", L = "abcdefghijkmnpqrstuvwxyz", D = "23456789", S = "!@#$%&*?";
  const all = U + L + D + S;
  const pick = (s: string) => s[Math.floor(Math.random() * s.length)];
  let p = pick(U) + pick(L) + pick(D) + pick(S);
  for (let i = 0; i < 8; i++) p += pick(all);
  return p.split("").sort(() => Math.random() - 0.5).join("");
}

const ROLES = [
  "Administrador", "QA", "Desarrollador", "LiderTecnico",
  "DevOps", "ProductOwner", "Auditor", "Cliente",
];

interface User {
  id: string;
  email: string;
  fullName: string;
  isActive: boolean;
  lastLoginAt?: string;
  roles: string[];
}

export default function UsersPage() {
  const { auth } = useAuth();
  const isSelf = (u: User) => u.email.toLowerCase() === auth?.email?.toLowerCase();
  const [users, setUsers] = useState<User[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);

  const [createOpen, setCreateOpen] = useState(false);
  const [form, setForm] = useState({ email: "", fullName: "", password: "", roles: ["QA"] as string[] });

  const [rolesUser, setRolesUser] = useState<User | null>(null);
  const [rolesDraft, setRolesDraft] = useState<string[]>([]);

  const [resetUser, setResetUser] = useState<User | null>(null);
  const [resetPwd, setResetPwd] = useState("");
  const [resetDone, setResetDone] = useState(false);

  const load = () =>
    api.get<Paged<User>>("/admin/users", { params: { page: 1, pageSize: 100 } })
      .then((r) => setUsers(r.data.items))
      .catch(() => setError("No fue posible cargar los usuarios (¿tu usuario es Administrador?)."));

  useEffect(() => { void load(); }, []);

  const createUser = async (e: FormEvent) => {
    e.preventDefault();
    setError(null); setInfo(null);
    try {
      await api.post("/auth/register", { ...form });
      setCreateOpen(false);
      setForm({ email: "", fullName: "", password: "", roles: ["QA"] });
      setInfo("Usuario creado correctamente.");
      void load();
    } catch (err: unknown) {
      setError((err as { response?: { data?: { error?: string } } }).response?.data?.error ?? "No fue posible crear el usuario.");
    }
  };

  const setActive = async (u: User, active: boolean) => {
    setError(null); setInfo(null);
    try {
      await api.put(`/admin/users/${u.id}/active`, { userId: u.id, active });
      setInfo(active ? "Usuario activado." : "Usuario desactivado.");
      void load();
    } catch (err: unknown) {
      setError((err as { response?: { data?: { error?: string } } }).response?.data?.error ?? "No fue posible cambiar el estado.");
    }
  };

  const deleteUser = async (u: User) => {
    if (!window.confirm(`¿Eliminar a "${u.fullName}" (${u.email})? Queda desactivado y oculto de la lista.`)) return;
    setError(null); setInfo(null);
    try {
      await api.delete(`/admin/users/${u.id}`);
      setInfo("Usuario eliminado.");
      void load();
    } catch (err: unknown) {
      setError((err as { response?: { data?: { error?: string } } }).response?.data?.error ?? "No fue posible eliminar el usuario.");
    }
  };

  const openReset = (u: User) => { setResetUser(u); setResetPwd(""); setResetDone(false); setError(null); };

  const saveReset = async () => {
    if (!resetUser) return;
    setError(null);
    try {
      await api.post(`/admin/users/${resetUser.id}/reset-password`, { userId: resetUser.id, newPassword: resetPwd });
      setResetDone(true);
    } catch (err: unknown) {
      setError((err as { response?: { data?: { error?: string } } }).response?.data?.error ?? "No fue posible restablecer la contraseña.");
    }
  };

  const saveRoles = async () => {
    if (!rolesUser) return;
    setError(null); setInfo(null);
    try {
      await api.put(`/admin/users/${rolesUser.id}/roles`, rolesDraft);
      setRolesUser(null);
      setInfo("Roles actualizados.");
      void load();
    } catch (err: unknown) {
      setError((err as { response?: { data?: { error?: string } } }).response?.data?.error ?? "No fue posible actualizar los roles.");
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <Box className="flex items-center justify-between">
        <Typography variant="h5" fontWeight={700}>Usuarios</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setCreateOpen(true)}>
          Nuevo usuario
        </Button>
      </Box>

      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}
      {info && <Alert severity="success" onClose={() => setInfo(null)}>{info}</Alert>}

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Correo</TableCell>
              <TableCell>Nombre</TableCell>
              <TableCell>Roles</TableCell>
              <TableCell align="center">Estado</TableCell>
              <TableCell>Último acceso</TableCell>
              <TableCell align="right">Acciones</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {users.map((u) => (
              <TableRow key={u.id} hover>
                <TableCell>{u.email}</TableCell>
                <TableCell>{u.fullName}</TableCell>
                <TableCell>
                  <Box className="flex flex-wrap gap-1">
                    {u.roles.map((r) => <Chip key={r} label={r} size="small" variant="outlined" />)}
                  </Box>
                </TableCell>
                <TableCell align="center">
                  <Chip label={u.isActive ? "Activo" : "Inactivo"} size="small"
                    color={u.isActive ? "success" : "default"} />
                </TableCell>
                <TableCell>{u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : "—"}</TableCell>
                <TableCell align="right">
                  <Tooltip title="Editar permisos (roles)">
                    <IconButton size="small" onClick={() => { setRolesUser(u); setRolesDraft(u.roles); }}>
                      <BadgeIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Restablecer contraseña">
                    <IconButton size="small" onClick={() => openReset(u)}>
                      <VpnKeyIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                  {!isSelf(u) && (
                    <Tooltip title={u.isActive ? "Desactivar" : "Activar"}>
                      <IconButton size="small" color={u.isActive ? "warning" : "success"}
                        onClick={() => void setActive(u, !u.isActive)}>
                        {u.isActive ? <BlockIcon fontSize="small" /> : <CheckCircleOutlineIcon fontSize="small" />}
                      </IconButton>
                    </Tooltip>
                  )}
                  {!isSelf(u) && (
                    <Tooltip title="Eliminar">
                      <IconButton size="small" color="error" onClick={() => void deleteUser(u)}>
                        <DeleteOutlineIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                  )}
                </TableCell>
              </TableRow>
            ))}
            {users.length === 0 && (
              <TableRow><TableCell colSpan={6}><Typography variant="body2" color="text.secondary" className="p-2">Sin usuarios.</Typography></TableCell></TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Crear usuario */}
      <Dialog open={createOpen} onClose={() => setCreateOpen(false)} maxWidth="sm" fullWidth>
        <form onSubmit={createUser}>
          <DialogTitle>Nuevo usuario</DialogTitle>
          <DialogContent className="flex flex-col gap-4 pt-2">
            <TextField label="Correo" type="email" required
              value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
            <TextField label="Nombre completo" required
              value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} />
            <TextField label="Contraseña" type="password" required
              value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })}
              helperText="Mínimo 10 caracteres, con mayúscula, minúscula y dígito." />
            <Select multiple value={form.roles} size="small"
              onChange={(e: SelectChangeEvent<string[]>) =>
                setForm({ ...form, roles: typeof e.target.value === "string" ? e.target.value.split(",") : e.target.value })}
              renderValue={(sel) => (sel as string[]).join(", ")}>
              {ROLES.map((r) => <MenuItem key={r} value={r}>{r}</MenuItem>)}
            </Select>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setCreateOpen(false)}>Cancelar</Button>
            <Button type="submit" variant="contained" disabled={form.roles.length === 0}>Crear</Button>
          </DialogActions>
        </form>
      </Dialog>

      {/* Editar roles */}
      <Dialog open={!!rolesUser} onClose={() => setRolesUser(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Roles de {rolesUser?.fullName}</DialogTitle>
        <DialogContent className="pt-2">
          <Select multiple fullWidth value={rolesDraft} size="small"
            onChange={(e: SelectChangeEvent<string[]>) =>
              setRolesDraft(typeof e.target.value === "string" ? e.target.value.split(",") : e.target.value)}
            renderValue={(sel) => (sel as string[]).join(", ")}>
            {ROLES.map((r) => <MenuItem key={r} value={r}>{r}</MenuItem>)}
          </Select>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRolesUser(null)}>Cancelar</Button>
          <Button variant="contained" disabled={rolesDraft.length === 0} onClick={() => void saveRoles()}>Guardar</Button>
        </DialogActions>
      </Dialog>

      {/* Restablecer contraseña */}
      <Dialog open={!!resetUser} onClose={() => setResetUser(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Restablecer contraseña</DialogTitle>
        <DialogContent className="flex flex-col gap-3 pt-2">
          <Typography variant="body2" color="text.secondary">
            Usuario: <b>{resetUser?.fullName}</b> ({resetUser?.email})
          </Typography>
          <Typography variant="caption" color="text.secondary">
            Por seguridad las contraseñas no se pueden ver, solo asignar una nueva. Comunícasela al usuario por un
            medio seguro y pídele que la cambie en su primer ingreso.
          </Typography>

          {resetDone ? (
            <Alert severity="success">
              Contraseña restablecida. Nueva contraseña (cópiala ahora, no volverá a mostrarse):
              <Box component="code" className="block mt-1 p-2 rounded bg-black/5 break-all text-sm">{resetPwd}</Box>
            </Alert>
          ) : (
            <>
              <TextField label="Nueva contraseña" value={resetPwd}
                onChange={(e) => setResetPwd(e.target.value)}
                helperText="Mínimo 10 caracteres, con mayúscula, minúscula y dígito." />
              <Button size="small" startIcon={<AutorenewIcon />} onClick={() => setResetPwd(generatePassword())}>
                Generar contraseña segura
              </Button>
            </>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setResetUser(null)}>{resetDone ? "Cerrar" : "Cancelar"}</Button>
          {!resetDone && (
            <Button variant="contained" disabled={!resetPwd} onClick={() => void saveReset()}>Restablecer</Button>
          )}
        </DialogActions>
      </Dialog>
    </div>
  );
}
