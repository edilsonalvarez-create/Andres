import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import {
  Alert, Box, Button, Card, CardContent, IconButton, InputAdornment, TextField, Typography,
} from "@mui/material";
import ShieldIcon from "@mui/icons-material/Shield";
import Visibility from "@mui/icons-material/Visibility";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import { useAuth } from "../auth/AuthContext";
import { extractApiErrorMessage } from "../api/client";

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await login(email, password);
      navigate("/");
    } catch (err: unknown) {
      // Mensaje genérico ante 401 (OWASP A07). Si la API no responde, orientar distinto.
      const status = (err as { response?: { status?: number; data?: { error?: string } } })?.response?.status;
      const apiError = extractApiErrorMessage(err);
      if (!status) {
        setError("No hay conexión con la API. Compruebe que el backend esté en http://localhost:5080.");
      } else if (apiError) {
        setError(apiError);
      } else {
        setError("Credenciales inválidas o cuenta bloqueada. Si el problema persiste, contacte a su administrador.");
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box className="flex min-h-screen items-center justify-center bg-guardian-900">
      <Card className="w-full max-w-md m-4" elevation={8}>
        <CardContent className="flex flex-col gap-4 p-8">
          <Box className="flex flex-col items-center gap-2">
            <ShieldIcon color="primary" sx={{ fontSize: 56 }} />
            <Typography variant="h5" fontWeight={700}>
              QA Guardian
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Quality Gate empresarial para sus despliegues
            </Typography>
          </Box>
          {error && (
            <Alert severity="error" role="alert" aria-live="assertive">
              {error}
            </Alert>
          )}
          <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
            <TextField
              label="Correo electrónico"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              fullWidth
              autoFocus
              autoComplete="email"
            />
            <TextField
              label="Contraseña"
              type={showPassword ? "text" : "password"}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              fullWidth
              autoComplete="current-password"
              slotProps={{
                input: {
                  endAdornment: (
                    <InputAdornment position="end">
                      <IconButton
                        aria-label={showPassword ? "Ocultar contraseña" : "Mostrar contraseña"}
                        onClick={() => setShowPassword((s) => !s)}
                        edge="end"
                      >
                        {showPassword ? <VisibilityOff /> : <Visibility />}
                      </IconButton>
                    </InputAdornment>
                  ),
                },
              }}
            />
            <Button type="submit" variant="contained" size="large" disabled={loading}>
              {loading ? "Verificando…" : "Iniciar sesión"}
            </Button>
          </form>
        </CardContent>
      </Card>
    </Box>
  );
}
