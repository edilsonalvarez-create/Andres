import { Box, TextField, MenuItem, Typography, Stack, Button, IconButton } from "@mui/material";
import { useState, useEffect } from "react";
import DeleteIcon from "@mui/icons-material/Delete";
import AddIcon from "@mui/icons-material/Add";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import FiberManualRecordIcon from "@mui/icons-material/FiberManualRecord";

interface PlaywrightFormProps {
  content: string;
  onChange: (content: string) => void;
  recordUrl: string;
  onRecordUrlChange: (url: string) => void;
  onRecord: () => void;
  busy: boolean;
}

type StepAction =
  | "goto"
  | "click"
  | "fill"
  | "check"
  | "select"
  | "press"
  | "wait"
  | "expect_visible"
  | "expect_text"
  | "expect_url";

interface Step {
  action: StepAction;
  target: string; // selector o URL
  value: string; // texto a escribir / valor esperado
}

interface PlaywrightConfig {
  testName: string;
  baseUrl: string;
  steps: Step[];
}

const ACTION_LABELS: Record<StepAction, string> = {
  goto: "Navegar a URL",
  click: "Hacer clic",
  fill: "Escribir texto",
  check: "Marcar checkbox",
  select: "Seleccionar opción",
  press: "Presionar tecla",
  wait: "Esperar (ms)",
  expect_visible: "Validar visible",
  expect_text: "Validar texto",
  expect_url: "Validar URL",
};

// Acciones que usan un selector como "target"
const SELECTOR_ACTIONS: StepAction[] = ["click", "fill", "check", "select", "expect_visible", "expect_text"];
// Acciones que usan un valor
const VALUE_ACTIONS: StepAction[] = ["fill", "select", "press", "wait", "expect_text", "expect_url"];

export default function PlaywrightForm({
  content,
  onChange,
  recordUrl,
  onRecordUrlChange,
  onRecord,
  busy,
}: PlaywrightFormProps) {
  const [config, setConfig] = useState<PlaywrightConfig>(defaultConfig());
  // Si el script no se puede representar como pasos (código grabado/complejo),
  // mostramos el editor de código en lugar del constructor visual.
  const [mode, setMode] = useState<"builder" | "code">("builder");

  useEffect(() => {
    if (content && content.trim()) {
      // Contenido existente: si fue grabado con codegen o es complejo,
      // lo mostramos como código para no perder información.
      setMode("code");
    } else {
      setMode("builder");
      const initial = defaultConfig();
      setConfig(initial);
      // Emitir el spec inicial para habilitar "Guardar"/"Guardar y ejecutar".
      onChange(generateSpec(initial));
    }
    // Solo al montar / cambiar de caso.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const updateConfig = (newConfig: PlaywrightConfig) => {
    setConfig(newConfig);
    onChange(generateSpec(newConfig));
  };

  const handleAddStep = () => {
    updateConfig({
      ...config,
      steps: [...config.steps, { action: "click", target: "", value: "" }],
    });
  };

  const handleRemoveStep = (index: number) => {
    updateConfig({ ...config, steps: config.steps.filter((_, i) => i !== index) });
  };

  const handleStepChange = (index: number, field: keyof Step, value: string) => {
    const newSteps = [...config.steps];
    newSteps[index] = { ...newSteps[index], [field]: value };
    updateConfig({ ...config, steps: newSteps });
  };

  const handleMoveStep = (index: number, dir: -1 | 1) => {
    const target = index + dir;
    if (target < 0 || target >= config.steps.length) return;
    const newSteps = [...config.steps];
    [newSteps[index], newSteps[target]] = [newSteps[target], newSteps[index]];
    updateConfig({ ...config, steps: newSteps });
  };

  return (
    <Stack spacing={3}>
      {/* Grabación con codegen */}
      <Box sx={{ p: 2, background: "#f0f9ff", borderRadius: 1, border: "1px solid #bae6fd" }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1, color: "#0369a1" }}>
          🎬 Grabar automáticamente (codegen)
        </Typography>
        <Typography variant="caption" sx={{ display: "block", mb: 1.5, color: "#0c4a6e" }}>
          Ingresa la URL, haz clic en "Grabar" e interactúa con tu app. Playwright generará el código por ti.
        </Typography>
        <Box sx={{ display: "flex", gap: 1.5, alignItems: "center" }}>
          <TextField
            label="URL a grabar"
            value={recordUrl}
            onChange={(e) => onRecordUrlChange(e.target.value)}
            placeholder="http://localhost:5173"
            size="small"
            sx={{ flex: 1 }}
          />
          <Button
            variant="outlined"
            color="error"
            startIcon={<FiberManualRecordIcon />}
            disabled={busy}
            onClick={onRecord}
          >
            Grabar
          </Button>
        </Box>
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
          Editar código
        </Button>
      </Box>

      {mode === "builder" ? (
        <>
          {/* Datos del test */}
          <Box sx={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 2 }}>
            <TextField
              label="Nombre del test"
              value={config.testName}
              onChange={(e) => updateConfig({ ...config, testName: e.target.value })}
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

          {/* Pasos */}
          <Box>
            <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 2 }}>
              <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>
                Pasos del test
              </Typography>
              <Button size="small" startIcon={<AddIcon />} onClick={handleAddStep} variant="outlined">
                Agregar paso
              </Button>
            </Box>

            <Stack spacing={1.5}>
              {config.steps.map((step, idx) => (
                <Box
                  key={idx}
                  sx={{ p: 1.5, background: "#f9fafb", borderRadius: 1, border: "1px solid #e5e7eb" }}
                >
                  <Box sx={{ display: "flex", gap: 1, alignItems: "flex-start" }}>
                    <Box
                      sx={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        minWidth: 26,
                        height: 26,
                        borderRadius: "50%",
                        background: "#2563eb",
                        color: "white",
                        fontSize: 12,
                        fontWeight: 700,
                        mt: 0.5,
                      }}
                    >
                      {idx + 1}
                    </Box>

                    <Box sx={{ flex: 1, display: "grid", gridTemplateColumns: "1fr", gap: 1 }}>
                      <TextField
                        select
                        label="Acción"
                        value={step.action}
                        onChange={(e) => handleStepChange(idx, "action", e.target.value)}
                        size="small"
                      >
                        {(Object.keys(ACTION_LABELS) as StepAction[]).map((a) => (
                          <MenuItem key={a} value={a}>
                            {ACTION_LABELS[a]}
                          </MenuItem>
                        ))}
                      </TextField>

                      <Box sx={{ display: "flex", gap: 1 }}>
                        {step.action === "goto" && (
                          <TextField
                            label="URL"
                            value={step.target}
                            onChange={(e) => handleStepChange(idx, "target", e.target.value)}
                            placeholder="/login"
                            size="small"
                            sx={{ flex: 1 }}
                          />
                        )}
                        {step.action === "expect_url" && (
                          <TextField
                            label="URL esperada"
                            value={step.value}
                            onChange={(e) => handleStepChange(idx, "value", e.target.value)}
                            placeholder="/dashboard"
                            size="small"
                            sx={{ flex: 1 }}
                          />
                        )}
                        {step.action === "press" && (
                          <TextField
                            label="Tecla"
                            value={step.value}
                            onChange={(e) => handleStepChange(idx, "value", e.target.value)}
                            placeholder="Enter"
                            size="small"
                            sx={{ flex: 1 }}
                          />
                        )}
                        {step.action === "wait" && (
                          <TextField
                            label="Milisegundos"
                            type="number"
                            value={step.value}
                            onChange={(e) => handleStepChange(idx, "value", e.target.value)}
                            placeholder="1000"
                            size="small"
                            sx={{ flex: 1 }}
                          />
                        )}
                        {SELECTOR_ACTIONS.includes(step.action) && (
                          <TextField
                            label="Selector"
                            value={step.target}
                            onChange={(e) => handleStepChange(idx, "target", e.target.value)}
                            placeholder="#email  o  text=Ingresar"
                            size="small"
                            sx={{ flex: 1 }}
                          />
                        )}
                        {VALUE_ACTIONS.includes(step.action) && step.action !== "press" &&
                          step.action !== "wait" && step.action !== "expect_url" && (
                            <TextField
                              label={step.action === "expect_text" ? "Texto esperado" : "Valor"}
                              value={step.value}
                              onChange={(e) => handleStepChange(idx, "value", e.target.value)}
                              placeholder={step.action === "fill" ? "usuario@test.com" : "..."}
                              size="small"
                              sx={{ flex: 1 }}
                            />
                          )}
                      </Box>
                    </Box>

                    <Stack>
                      <IconButton size="small" onClick={() => handleMoveStep(idx, -1)} disabled={idx === 0}>
                        <ArrowUpwardIcon fontSize="small" />
                      </IconButton>
                      <IconButton
                        size="small"
                        onClick={() => handleMoveStep(idx, 1)}
                        disabled={idx === config.steps.length - 1}
                      >
                        <ArrowDownwardIcon fontSize="small" />
                      </IconButton>
                      <IconButton size="small" onClick={() => handleRemoveStep(idx)} sx={{ color: "#dc2626" }}>
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </Stack>
                  </Box>
                </Box>
              ))}

              {config.steps.length === 0 && (
                <Box sx={{ textAlign: "center", py: 3, color: "#9ca3af" }}>
                  <Typography variant="caption">
                    No hay pasos. Haz clic en "Agregar paso" para comenzar.
                  </Typography>
                </Box>
              )}
            </Stack>
          </Box>

          {/* Vista previa */}
          <Box sx={{ p: 2, background: "#f9fafb", borderRadius: 1, border: "1px solid #e5e7eb" }}>
            <Typography variant="caption" sx={{ fontWeight: 600, display: "block", mb: 1 }}>
              Preview del código Playwright:
            </Typography>
            <pre style={{ fontSize: 11, margin: 0, overflow: "auto", maxHeight: 200, color: "#4b5563" }}>
              {generateSpec(config)}
            </pre>
          </Box>

          {/* Ayuda de selectores */}
          <Box sx={{ p: 2, background: "#fef3c7", borderRadius: 1, border: "1px solid #fcd34d" }}>
            <Typography variant="caption" sx={{ display: "block", fontWeight: 600, mb: 1, color: "#b45309" }}>
              💡 Ejemplos de selectores
            </Typography>
            <Typography variant="caption" sx={{ display: "block", color: "#92400e", fontFamily: "monospace" }}>
              #email → por id • .btn → por clase • text=Ingresar → por texto<br />
              [name=password] → por atributo • button → por etiqueta
            </Typography>
          </Box>
        </>
      ) : (
        <TextField
          label="Contenido del script"
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

function defaultConfig(): PlaywrightConfig {
  return {
    testName: "Nuevo test",
    baseUrl: "http://localhost:5173",
    steps: [
      { action: "goto", target: "/", value: "" },
    ],
  };
}

function generateSpec(config: PlaywrightConfig): string {
  const lines: string[] = [];
  lines.push(`import { test, expect } from '@playwright/test';`);
  lines.push("");
  lines.push(`test('${(config.testName || "test").replace(/'/g, "\\'")}', async ({ page }) => {`);

  const base = (config.baseUrl || "").replace(/\/$/, "");

  for (const step of config.steps) {
    const t = step.target;
    const v = step.value;
    switch (step.action) {
      case "goto": {
        const path = t.startsWith("http") ? t : `${base}${t}`;
        lines.push(`  await page.goto('${path}');`);
        break;
      }
      case "click":
        lines.push(`  await page.click('${t}');`);
        break;
      case "fill":
        lines.push(`  await page.fill('${t}', '${v}');`);
        break;
      case "check":
        lines.push(`  await page.check('${t}');`);
        break;
      case "select":
        lines.push(`  await page.selectOption('${t}', '${v}');`);
        break;
      case "press":
        lines.push(`  await page.keyboard.press('${v}');`);
        break;
      case "wait":
        lines.push(`  await page.waitForTimeout(${v || 1000});`);
        break;
      case "expect_visible":
        lines.push(`  await expect(page.locator('${t}')).toBeVisible();`);
        break;
      case "expect_text":
        lines.push(`  await expect(page.locator('${t}')).toContainText('${v}');`);
        break;
      case "expect_url":
        lines.push(`  await expect(page).toHaveURL(/${(v || "").replace(/\//g, "\\/")}/);`);
        break;
    }
  }

  lines.push(`});`);
  return lines.join("\n");
}
