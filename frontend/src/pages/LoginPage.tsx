import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { Alert, Box, Button, Card, CardContent, TextField, Typography } from "@mui/material";
import ShieldIcon from "@mui/icons-material/Shield";
import { useAuth } from "../auth/AuthContext";

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await login(email, password);
      navigate("/");
    } catch {
      setError("Credenciales inválidas o cuenta bloqueada.");
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
          {error && <Alert severity="error">{error}</Alert>}
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <TextField
              label="Correo electrónico"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              fullWidth
              autoFocus
            />
            <TextField
              label="Contraseña"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              fullWidth
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
