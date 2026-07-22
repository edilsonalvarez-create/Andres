import { Box, TextField, MenuItem, Typography, Stack, Button, IconButton } from "@mui/material";
import { useState, useEffect } from "react";
import DeleteIcon from "@mui/icons-material/Delete";
import AddIcon from "@mui/icons-material/Add";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";

interface SeleniumIDEFormProps {
  content: string;
  onChange: (content: string) => void;
}

interface Command {
  id: string;
  command: string;
  target: string;
  value: string;
}

interface SeleniumConfig {
  name: string;
  baseUrl: string;
  commands: Command[];
}

// Comandos más usados de Selenium IDE (la extensión soporta muchos más;
// para casos avanzados está el modo "Editar código" con el .side completo).
const SELENIUM_COMMANDS = [
  "open",
  "click",
  "clickAt",
  "doubleClick",
  "type",
  "sendKeys",
  "select",
  "addSelection",
  "check",
  "uncheck",
  "mouseOver",
  "mouseOut",
  "pause",
  "setWindowSize",
  "runScript",
  "assertText",
  "verifyText",
  "assertValue",
  "assertTitle",
  "assertElementPresent",
  "assertElementNotPresent",
  "assertChecked",
  "waitForElementVisible",
  "waitForElementPresent",
  "waitForText",
  "storeText",
  "storeValue",
  "echo",
];

const uuid = () =>
  typeof crypto !== "undefined" && "randomUUID" in crypto
    ? crypto.randomUUID()
    : Math.random().toString(36).slice(2) + Date.now().toString(36);

export default function SeleniumIDEForm({ content, onChange }: SeleniumIDEFormProps) {
  const [config, setConfig] = useState<SeleniumConfig>(defaultConfig());
  const [mode, setMode] = useState<"builder" | "code">("builder");
  // Ids estables de la estructura .side (no se regeneran en cada edición).
  const [ids, setIds] = useState(() => ({
    projectId: uuid(),
    testId: uuid(),
    suiteId: uuid(),
  }));

  useEffect(() => {
    if (content && content.trim()) {
      // Intentar reconstruir el constructor desde un .side existente.
      try {
        const parsed = JSON.parse(content);
        const test = parsed?.tests?.[0];
        if (test) {
          setConfig({
            name: test.name || "Nuevo test",
            baseUrl: parsed.url || "",
            commands: (test.commands || []).map((c: Record<string, string>) => ({
              id: c.id || uuid(),
              command: c.command || "open",
              target: c.target || "",
              value: c.value || "",
            })),
          });
          setIds({
            projectId: parsed.id || uuid(),
            testId: test.id || uuid(),
            suiteId: parsed?.suites?.[0]?.id || uuid(),
          });
          setMode("builder");
          return;
        }
      } catch {
        /* contenido no parseable como .side */
      }
      setMode("code");
    } else {
      // Script nuevo: emitir el .side inicial para que "Guardar" se habilite
      // aunque el usuario no toque el constructor.
      onChange(generateSide(config, ids));
    }
    // Solo al montar / cambiar de caso.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const updateConfig = (next: SeleniumConfig) => {
    setConfig(next);
    onChange(generateSide(next, ids));
  };

  const addCommand = () => {
    updateConfig({
      ...config,
      commands: [...config.commands, { id: uuid(), command: "click", target: "", value: "" }],
    });
  };

  const removeCommand = (index: number) => {
    updateConfig({ ...config, commands: config.commands.filter((_, i) => i !== index) });
  };

  const changeCommand = (index: number, field: keyof Command, value: string) => {
    const commands = [...config.commands];
    commands[index] = { ...commands[index], [field]: value };
    updateConfig({ ...config, commands });
  };

  const moveCommand = (index: number, dir: -1 | 1) => {
    const target = index + dir;
    if (target < 0 || target >= config.commands.length) return;
    const commands = [...config.commands];
    [commands[index], commands[target]] = [commands[target], commands[index]];
    updateConfig({ ...config, commands });
  };

  return (
    <Stack spacing={3}>
      {/* Info del formato */}
      <Box sx={{ p: 2, background: "#f0fdf4", borderRadius: 1, border: "1px solid #bbf7d0" }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 0.5, color: "#15803d" }}>
          🧪 Selenium IDE (.side)
        </Typography>
        <Typography variant="caption" sx={{ display: "block", color: "#166534" }}>
          Construye la prueba como en Selenium IDE: una tabla de <strong>Comando · Objetivo · Valor</strong>.
          El resultado es un archivo <code>.side</code> que puedes importar/exportar en la extensión Selenium IDE.
        </Typography>
      </Box>

      {/* Selector de modo */}
      <Box sx={{ display: "flex", gap: 1 }}>
        <Button
          variant={mode === "builder" ? "contained" : "outlined"}
          size="small"
          onClick={() => setMode("builder")}
        >
          Constructor visual
        </Button>
        <Button
          variant={mode === "code" ? "contained" : "outlined"}
          size="small"
          onClick={() => setMode("code")}
        >
          Editar código (.side)
        </Button>
      </Box>

      {mode === "builder" ? (
        <>
          {/* Datos del test */}
          <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2 }}>
            <TextField
              label="Nombre del test"
              value={config.name}
              onChange={(e) => updateConfig({ ...config, name: e.target.value })}
              placeholder="Login exitoso"
              size="small"
            />
            <TextField
              label="URL base"
              value={config.baseUrl}
              onChange={(e) => updateConfig({ ...config, baseUrl: e.target.value })}
              placeholder="http://localhost:5173"
              size="small"
            />
          </Box>

          {/* Tabla de comandos */}
          <Box>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                Comandos
              </Typography>
              <Button size="small" startIcon={<AddIcon />} onClick={addCommand} variant="outlined">
                Agregar comando
              </Button>
            </Box>

            {/* Encabezado de columnas */}
            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: "28px 190px 1fr 1fr 96px",
                gap: 1,
                px: 0.5,
                mb: 0.5,
              }}
            >
              <Typography variant="caption" sx={{ fontWeight: 700, color: "#6b7280" }}>
                #
              </Typography>
              <Typography variant="caption" sx={{ fontWeight: 700, color: "#6b7280" }}>
                Comando
              </Typography>
              <Typography variant="caption" sx={{ fontWeight: 700, color: "#6b7280" }}>
                Objetivo (locator)
              </Typography>
              <Typography variant="caption" sx={{ fontWeight: 700, color: "#6b7280" }}>
                Valor
              </Typography>
              <Typography variant="caption" sx={{ fontWeight: 700, color: "#6b7280" }} />
            </Box>

            <Stack spacing={1}>
              {config.commands.map((cmd, idx) => (
                <Box
                  key={cmd.id}
                  sx={{
                    display: "grid",
                    gridTemplateColumns: "28px 190px 1fr 1fr 96px",
                    gap: 1,
                    alignItems: "center",
                    p: 0.5,
                    background: "#f9fafb",
                    borderRadius: 1,
                    border: "1px solid #e5e7eb",
                  }}
                >
                  <Box
                    sx={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      width: 22,
                      height: 22,
                      borderRadius: "50%",
                      background: "#16a34a",
                      color: "white",
                      fontSize: 11,
                      fontWeight: 700,
                    }}
                  >
                    {idx + 1}
                  </Box>

                  <TextField
                    select
                    value={cmd.command}
                    onChange={(e) => changeCommand(idx, "command", e.target.value)}
                    size="small"
                  >
                    {SELENIUM_COMMANDS.map((c) => (
                      <MenuItem key={c} value={c} sx={{ fontFamily: "monospace", fontSize: 13 }}>
                        {c}
                      </MenuItem>
                    ))}
                  </TextField>

                  <TextField
                    value={cmd.target}
                    onChange={(e) => changeCommand(idx, "target", e.target.value)}
                    placeholder={cmd.command === "open" ? "/login" : "id=email"}
                    size="small"
                    slotProps={{ input: { style: { fontFamily: "monospace", fontSize: 12 } } }}
                  />

                  <TextField
                    value={cmd.value}
                    onChange={(e) => changeCommand(idx, "value", e.target.value)}
                    placeholder={cmd.command === "type" ? "usuario@test.com" : ""}
                    size="small"
                    slotProps={{ input: { style: { fontFamily: "monospace", fontSize: 12 } } }}
                  />

                  <Box sx={{ display: "flex" }}>
                    <IconButton size="small" onClick={() => moveCommand(idx, -1)} disabled={idx === 0}>
                      <ArrowUpwardIcon fontSize="small" />
                    </IconButton>
                    <IconButton
                      size="small"
                      onClick={() => moveCommand(idx, 1)}
                      disabled={idx === config.commands.length - 1}
                    >
                      <ArrowDownwardIcon fontSize="small" />
                    </IconButton>
                    <IconButton size="small" onClick={() => removeCommand(idx)} sx={{ color: "#dc2626" }}>
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Box>
                </Box>
              ))}

              {config.commands.length === 0 && (
                <Box sx={{ textAlign: "center", py: 3, color: "#9ca3af" }}>
                  <Typography variant="caption">
                    No hay comandos. Haz clic en "Agregar comando" para comenzar.
                  </Typography>
                </Box>
              )}
            </Stack>
          </Box>

          {/* Ayuda de locators */}
          <Box sx={{ p: 2, background: "#fef3c7", borderRadius: 1, border: "1px solid #fcd34d" }}>
            <Typography variant="caption" sx={{ display: "block", fontWeight: 600, mb: 1, color: "#b45309" }}>
              💡 Formato de locators (Objetivo)
            </Typography>
            <Typography variant="caption" sx={{ display: "block", color: "#92400e", fontFamily: "monospace" }}>
              id=email · name=password · css=.btn-primary · xpath=//button[@type='submit'] · linkText=Ingresar
            </Typography>
          </Box>

          {/* Vista previa */}
          <Box sx={{ p: 2, background: "#f9fafb", borderRadius: 1, border: "1px solid #e5e7eb" }}>
            <Typography variant="caption" sx={{ fontWeight: 600, display: "block", mb: 1 }}>
              Preview del archivo .side:
            </Typography>
            <pre style={{ fontSize: 11, margin: 0, overflow: "auto", maxHeight: 220, color: "#4b5563" }}>
              {generateSide(config, ids)}
            </pre>
          </Box>
        </>
      ) : (
        <TextField
          label="Contenido del script (.side)"
          value={content}
          onChange={(e) => onChange(e.target.value)}
          multiline
          minRows={16}
          fullWidth
          slotProps={{ input: { style: { fontFamily: "Consolas, 'Courier New', monospace", fontSize: 13 } } }}
        />
      )}
    </Stack>
  );
}

function defaultConfig(): SeleniumConfig {
  return {
    name: "Nuevo test",
    baseUrl: "http://localhost:5173",
    commands: [{ id: uuid(), command: "open", target: "/", value: "" }],
  };
}

function generateSide(
  cfg: SeleniumConfig,
  ids: { projectId: string; testId: string; suiteId: string }
): string {
  const side = {
    id: ids.projectId,
    version: "2.0",
    name: cfg.name || "QA Guardian Suite",
    url: cfg.baseUrl || "",
    tests: [
      {
        id: ids.testId,
        name: cfg.name || "Nuevo test",
        commands: cfg.commands.map((c) => ({
          id: c.id,
          comment: "",
          command: c.command,
          target: c.target,
          targets: [],
          value: c.value,
        })),
      },
    ],
    suites: [
      {
        id: ids.suiteId,
        name: "Default Suite",
        persistSession: false,
        parallel: false,
        timeout: 300,
        tests: [ids.testId],
      },
    ],
    urls: cfg.baseUrl ? [cfg.baseUrl] : [],
    plugins: [],
  };
  return JSON.stringify(side, null, 2);
}
