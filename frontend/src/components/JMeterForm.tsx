import { Box, TextField, MenuItem, Typography, Stack, Button } from "@mui/material";
import DeleteIcon from "@mui/icons-material/Delete";
import AddIcon from "@mui/icons-material/Add";
import { useParsedYamlConfig } from "../hooks/useParsedYamlConfig";

interface JMeterFormProps {
  content: string;
  onChange: (content: string) => void;
}

interface Sampler {
  name: string;
  method: string;
  url: string;
}

interface JMeterConfig {
  threads: number;
  rampup: number;
  duration: number;
  samplers: Sampler[];
}

export default function JMeterForm({ content, onChange }: JMeterFormProps) {
  const [config, updateConfig] = useParsedYamlConfig(
    content, onChange, defaultConfig, parseYaml, generateYaml
  );

  const handleAddSampler = () => {
    updateConfig({
      ...config,
      samplers: [...config.samplers, { name: "", method: "GET", url: "" }],
    });
  };

  const handleRemoveSampler = (index: number) => {
    updateConfig({
      ...config,
      samplers: config.samplers.filter((_, i) => i !== index),
    });
  };

  const handleSamplerChange = (index: number, field: keyof Sampler, value: string) => {
    const newSamplers = [...config.samplers];
    newSamplers[index][field] = value;
    updateConfig({ ...config, samplers: newSamplers });
  };

  return (
    <Stack spacing={3}>
      {/* Configuración de Carga */}
      <Box sx={{ p: 2, background: "#eff6ff", borderRadius: 1, border: "1px solid #bfdbfe" }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 2 }}>Configuración de Carga</Typography>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 2 }}>
          <Box>
            <TextField
              label="Usuarios Simultáneos"
              type="number"
              value={config.threads}
              onChange={(e) => updateConfig({ ...config, threads: Number(e.target.value) })}
              size="small"
              fullWidth
              helperText="threads"
            />
          </Box>
          <Box>
            <TextField
              label="Rampup (segundos)"
              type="number"
              value={config.rampup}
              onChange={(e) => updateConfig({ ...config, rampup: Number(e.target.value) })}
              size="small"
              fullWidth
              helperText="Tiempo para aumentar"
            />
          </Box>
          <Box>
            <TextField
              label="Duración (segundos)"
              type="number"
              value={config.duration}
              onChange={(e) => updateConfig({ ...config, duration: Number(e.target.value) })}
              size="small"
              fullWidth
              helperText="Cuánto dura el test"
            />
          </Box>
        </Box>

        <Typography variant="caption" sx={{ display: "block", mt: 2, color: "#0c4a6e" }}>
          💡 Tip: Con threads=50, rampup=10: agrega 5 usuarios por segundo<br/>
          Ejemplo: 10 usuarios → espera 2 segundos, 20 usuarios → espera 2 seg más...
        </Typography>
      </Box>

      {/* Samplers (Endpoints) */}
      <Box>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>Endpoints a Probar</Typography>
          <Button size="small" startIcon={<AddIcon />} onClick={handleAddSampler} variant="outlined">
            Agregar Endpoint
          </Button>
        </Box>

        <Stack spacing={2}>
          {config.samplers.map((sampler, idx) => (
            <Box key={idx} sx={{ p: 2, background: "#f9fafb", borderRadius: 1, border: "1px solid #e5e7eb" }}>
              <Box sx={{ display: "grid", gridTemplateColumns: "1fr auto", gap: 2, mb: 2 }}>
                <TextField
                  label="Nombre del Sampler"
                  value={sampler.name}
                  onChange={(e) => handleSamplerChange(idx, "name", e.target.value)}
                  placeholder="ej: Login API"
                  size="small"
                  fullWidth
                />
                <Button
                  size="small"
                  onClick={() => handleRemoveSampler(idx)}
                  sx={{ color: "#dc2626" }}
                >
                  <DeleteIcon fontSize="small" />
                </Button>
              </Box>

              <Box sx={{ display: "grid", gridTemplateColumns: "120px 1fr", gap: 2 }}>
                <TextField
                  select
                  label="Método"
                  value={sampler.method}
                  onChange={(e) => handleSamplerChange(idx, "method", e.target.value)}
                  size="small"
                >
                  {["GET", "POST", "PUT", "PATCH", "DELETE"].map((m) => (
                    <MenuItem key={m} value={m}>{m}</MenuItem>
                  ))}
                </TextField>
                <TextField
                  label="URL"
                  value={sampler.url}
                  onChange={(e) => handleSamplerChange(idx, "url", e.target.value)}
                  placeholder="http://localhost:5000/api/users"
                  size="small"
                  fullWidth
                />
              </Box>
            </Box>
          ))}
        </Stack>
      </Box>

      {/* Vista previa */}
      <Box sx={{ p: 2, background: "#f9fafb", borderRadius: 1, border: "1px solid #e5e7eb" }}>
        <Typography variant="caption" sx={{ fontWeight: 600, display: "block", mb: 1 }}>Preview YAML:</Typography>
        <pre style={{ fontSize: 11, margin: 0, overflow: "auto", maxHeight: 200, color: "#4b5563" }}>
          {generateYaml(config)}
        </pre>
      </Box>

      {/* Información útil */}
      <Box sx={{ p: 2, background: "#fef3c7", borderRadius: 1, border: "1px solid #fcd34d" }}>
        <Typography variant="caption" sx={{ display: "block", fontWeight: 600, mb: 1, color: "#b45309" }}>
          📊 ¿Qué esperar?
        </Typography>
        <Typography variant="caption" sx={{ display: "block", color: "#92400e" }}>
          • Response Time Avg: tiempo promedio de respuesta<br/>
          • Max: la respuesta más lenta<br/>
          • Throughput: solicitudes por segundo<br/>
          • Error Rate: % de solicitudes fallidas
        </Typography>
      </Box>
    </Stack>
  );
}

function defaultConfig(): JMeterConfig {
  return {
    threads: 10,
    rampup: 5,
    duration: 30,
    samplers: [
      { name: "GET Health", method: "GET", url: "http://localhost:5000/api/health" }
    ],
  };
}

function parseYaml(content: string): JMeterConfig {
  const threadsMatch = content.match(/threads:\s*(\d+)/);
  const rampupMatch = content.match(/rampup:\s*(\d+)/);
  const durationMatch = content.match(/duration:\s*(\d+)/);

  return {
    threads: threadsMatch ? Number(threadsMatch[1]) : 10,
    rampup: rampupMatch ? Number(rampupMatch[1]) : 5,
    duration: durationMatch ? Number(durationMatch[1]) : 30,
    samplers: [],
  };
}

function generateYaml(config: JMeterConfig): string {
  let yaml = `testplan:
  name: "Load Test"
  threadgroups:
    - name: "${config.threads} Usuarios"
      threads: ${config.threads}
      rampup: ${config.rampup}
      duration: ${config.duration}

      samplers:`;

  config.samplers.forEach((sampler) => {
    if (sampler.url) {
      yaml += `\n        - name: "${sampler.name || sampler.method}"
          method: ${sampler.method}
          url: ${sampler.url}`;
    }
  });

  yaml += `\n
      assertions:
        - type: response_code
          value: 200`;

  return yaml;
}
