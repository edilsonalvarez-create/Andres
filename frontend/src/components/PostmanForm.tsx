import { Box, TextField, MenuItem, FormControlLabel, Checkbox, Typography, Stack, Button } from "@mui/material";
import { useState, useEffect } from "react";
import DeleteIcon from "@mui/icons-material/Delete";
import AddIcon from "@mui/icons-material/Add";

interface PostmanFormProps {
  content: string;
  onChange: (content: string) => void;
}

interface PostmanConfig {
  method: string;
  url: string;
  headers: { key: string; value: string }[];
  body?: string;
  expect_status: number;
  expect_json?: { [key: string]: string };
}

export default function PostmanForm({ content, onChange }: PostmanFormProps) {
  const [config, setConfig] = useState<PostmanConfig>(defaultConfig());

  useEffect(() => {
    if (content && content.trim()) {
      try {
        if (content.match(/^method:\s*(\S+)/m)) {
          setConfig(parseYaml(content) as PostmanConfig);
        }
      } catch {
        setConfig(defaultConfig());
      }
    } else {
      // Nuevo script: emitir el YAML inicial para habilitar Guardar/Ejecutar.
      onChange(generateYaml(config));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const [useBody, setUseBody] = useState(!!config.body);
  const [useAssertions, setUseAssertions] = useState(!!config.expect_json);

  const updateConfig = (newConfig: PostmanConfig) => {
    setConfig(newConfig);
    onChange(generateYaml(newConfig));
  };

  const handleAddHeader = () => {
    updateConfig({
      ...config,
      headers: [...config.headers, { key: "", value: "" }],
    });
  };

  const handleRemoveHeader = (index: number) => {
    updateConfig({
      ...config,
      headers: config.headers.filter((_, i) => i !== index),
    });
  };

  const handleHeaderChange = (index: number, field: "key" | "value", value: string) => {
    const newHeaders = [...config.headers];
    newHeaders[index][field] = value;
    updateConfig({ ...config, headers: newHeaders });
  };

  return (
    <Stack spacing={3}>
      {/* URL y Método */}
      <Box sx={{ display: "grid", gridTemplateColumns: "1fr 150px", gap: 2 }}>
        <TextField
          label="URL"
          value={config.url}
          onChange={(e) => updateConfig({ ...config, url: e.target.value })}
          placeholder="http://localhost:5000/api/users"
          fullWidth
          size="small"
        />
        <TextField
          select
          label="Método"
          value={config.method}
          onChange={(e) => updateConfig({ ...config, method: e.target.value })}
          size="small"
        >
          {["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD"].map((m) => (
            <MenuItem key={m} value={m}>{m}</MenuItem>
          ))}
        </TextField>
      </Box>

      {/* Headers */}
      <Box>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>Headers</Typography>
          <Button size="small" startIcon={<AddIcon />} onClick={handleAddHeader}>
            Agregar
          </Button>
        </Box>
        <Stack spacing={1}>
          {config.headers.map((header, idx) => (
            <Box key={idx} sx={{ display: "grid", gridTemplateColumns: "1fr 1fr 40px", gap: 1 }}>
              <TextField
                size="small"
                placeholder="Content-Type"
                value={header.key}
                onChange={(e) => handleHeaderChange(idx, "key", e.target.value)}
              />
              <TextField
                size="small"
                placeholder="application/json"
                value={header.value}
                onChange={(e) => handleHeaderChange(idx, "value", e.target.value)}
              />
              <Button
                size="small"
                onClick={() => handleRemoveHeader(idx)}
                sx={{ color: "#dc2626" }}
              >
                <DeleteIcon fontSize="small" />
              </Button>
            </Box>
          ))}
        </Stack>
      </Box>

      {/* Body */}
      <Box>
        <FormControlLabel
          control={<Checkbox checked={useBody} onChange={(e) => setUseBody(e.target.checked)} />}
          label="Incluir Body (JSON)"
        />
        {useBody && (
          <TextField
            value={config.body || ""}
            onChange={(e) => updateConfig({ ...config, body: e.target.value })}
            placeholder='{"email": "user@test.com", "password": "test123"}'
            multiline
            minRows={4}
            fullWidth
            size="small"
            sx={{ mt: 1, fontFamily: "monospace" }}
          />
        )}
      </Box>

      {/* Assertions */}
      <Box>
        <FormControlLabel
          control={<Checkbox checked={useAssertions} onChange={(e) => setUseAssertions(e.target.checked)} />}
          label="Validaciones (Assertions)"
        />
        {useAssertions && (
          <Box sx={{ mt: 1, p: 2, background: "#f3f4f6", borderRadius: 1 }}>
            <TextField
              label="Status HTTP esperado"
              type="number"
              value={config.expect_status}
              onChange={(e) => updateConfig({ ...config, expect_status: Number(e.target.value) })}
              size="small"
              fullWidth
              sx={{ mb: 2 }}
            />
            <Typography variant="caption" sx={{ display: "block", mb: 1, color: "#6b7280" }}>
              Respuesta JSON esperada (ej: "token: exists", "user.id: exists")
            </Typography>
          </Box>
        )}
      </Box>

      {/* Vista previa */}
      <Box sx={{ p: 2, background: "#f9fafb", borderRadius: 1, border: "1px solid #e5e7eb" }}>
        <Typography variant="caption" sx={{ fontWeight: 600, display: "block", mb: 1 }}>Preview YAML:</Typography>
        <pre style={{ fontSize: 11, margin: 0, overflow: "auto", maxHeight: 150, color: "#4b5563" }}>
          {generateYaml(config)}
        </pre>
      </Box>
    </Stack>
  );
}

function defaultConfig(): PostmanConfig {
  return {
    method: "GET",
    url: "http://localhost:5000/api/health",
    headers: [{ key: "Content-Type", value: "application/json" }],
    expect_status: 200,
  };
}

function parseYaml(content: string): PostmanConfig {
  const obj: PostmanConfig = {
    method: "GET",
    url: "",
    headers: [],
    expect_status: 200,
  };

  const methodMatch = content.match(/method:\s*(\S+)/);
  if (methodMatch) obj.method = methodMatch[1];

  const urlMatch = content.match(/url:\s*(.+)/);
  if (urlMatch) obj.url = urlMatch[1].trim();

  const statusMatch = content.match(/expect_status:\s*(\d+)/);
  if (statusMatch) obj.expect_status = Number(statusMatch[1]);

  return obj;
}

function generateYaml(config: PostmanConfig): string {
  let yaml = `method: ${config.method}\nurl: ${config.url}`;

  if (config.headers.length > 0) {
    yaml += "\nheaders:";
    config.headers.forEach((h) => {
      if (h.key && h.value) {
        yaml += `\n  ${h.key}: ${h.value}`;
      }
    });
  }

  if (config.body) {
    yaml += `\nbody: |\n  ${config.body.split("\n").join("\n  ")}`;
  }

  yaml += `\nexpect_status: ${config.expect_status}`;

  return yaml;
}
