import { Box, Tooltip, Typography } from "@mui/material";

interface Cell {
  moduleName: string;
  date: string;
  failedCount: number;
}

/** Heatmap de fallos módulo × día. La intensidad del color crece con el número de fallos. */
export default function FailureHeatmap({ cells }: { cells: Cell[] }) {
  if (cells.length === 0) {
    return <Typography variant="body2" color="text.secondary">Sin fallos en el rango seleccionado.</Typography>;
  }

  const modules = Array.from(new Set(cells.map((c) => c.moduleName))).sort();
  const dates = Array.from(new Set(cells.map((c) => c.date))).sort();
  const max = Math.max(...cells.map((c) => c.failedCount));
  const lookup = new Map(cells.map((c) => [`${c.moduleName}|${c.date}`, c.failedCount]));

  const color = (count: number) => {
    if (count === 0) return "#eef1f6";
    const intensity = 0.25 + 0.75 * (count / max); // 0.25–1.0
    return `rgba(198, 40, 40, ${intensity})`; // rojo QA
  };

  return (
    <Box className="overflow-x-auto">
      <table style={{ borderCollapse: "separate", borderSpacing: 3 }}>
        <thead>
          <tr>
            <th />
            {dates.map((d) => (
              <th key={d} style={{ fontSize: 10, fontWeight: 500, padding: "0 2px", whiteSpace: "nowrap" }}>
                {d.slice(5)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {modules.map((m) => (
            <tr key={m}>
              <td style={{ fontSize: 12, paddingRight: 8, whiteSpace: "nowrap", textAlign: "right" }}>{m}</td>
              {dates.map((d) => {
                const count = lookup.get(`${m}|${d}`) ?? 0;
                return (
                  <td key={d}>
                    <Tooltip title={`${m} · ${d}: ${count} fallo(s)`} arrow>
                      <div
                        style={{
                          width: 22, height: 22, borderRadius: 4,
                          background: color(count),
                          display: "flex", alignItems: "center", justifyContent: "center",
                          fontSize: 10, color: count > max * 0.5 ? "white" : "#555",
                        }}
                      >
                        {count > 0 ? count : ""}
                      </div>
                    </Tooltip>
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>
    </Box>
  );
}
