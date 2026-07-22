import { useEffect, useMemo, useState } from "react";
import {
  Dialog, List, ListItemButton, ListItemIcon, ListItemText, TextField, Typography,
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import InputAdornment from "@mui/material/InputAdornment";

export interface CommandItem {
  label: string;
  path: string;
  icon: React.ReactNode;
}

interface Props {
  open: boolean;
  onClose: () => void;
  items: CommandItem[];
  onSelect: (path: string) => void;
}

/**
 * Paleta de comandos (`Ctrl+K`/`Cmd+K`): salta a cualquier pantalla en 1 atajo + Enter, sin
 * usar el mouse ni recorrer el menú lateral. Nielsen N7 (flexibilidad y eficiencia para
 * usuarios expertos) — ver UX-UI-Audit.md L-01.
 */
export default function CommandPalette({ open, onClose, items, onSelect }: Props) {
  const [query, setQuery] = useState("");
  const [activeIndex, setActiveIndex] = useState(0);

  useEffect(() => {
    if (open) { setQuery(""); setActiveIndex(0); }
  }, [open]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return items;
    return items.filter((i) => i.label.toLowerCase().includes(q));
  }, [items, query]);

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setActiveIndex((i) => Math.min(i + 1, filtered.length - 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActiveIndex((i) => Math.max(i - 1, 0));
    } else if (e.key === "Enter" && filtered[activeIndex]) {
      e.preventDefault();
      onSelect(filtered[activeIndex].path);
    }
  };

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="xs"
      fullWidth
      slotProps={{ paper: { sx: { position: "fixed", top: 96 }, elevation: 12 } }}
    >
      <TextField
        autoFocus
        placeholder="Ir a… (ej. defectos, integraciones)"
        value={query}
        onChange={(e) => { setQuery(e.target.value); setActiveIndex(0); }}
        onKeyDown={handleKeyDown}
        variant="standard"
        sx={{ px: 2, pt: 2, pb: 1 }}
        slotProps={{
          input: {
            disableUnderline: true,
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon color="action" />
              </InputAdornment>
            ),
          },
        }}
        aria-label="Buscar una pantalla"
        aria-activedescendant={filtered[activeIndex] ? `cmd-item-${activeIndex}` : undefined}
        role="combobox"
        aria-expanded={open}
        aria-controls="command-palette-list"
      />
      <List id="command-palette-list" role="listbox" dense sx={{ maxHeight: 360, overflowY: "auto", pb: 1 }}>
        {filtered.length === 0 ? (
          <Typography variant="body2" color="text.secondary" sx={{ px: 3, py: 2 }}>
            Sin resultados para "{query}".
          </Typography>
        ) : (
          filtered.map((item, idx) => (
            <ListItemButton
              key={item.path}
              id={`cmd-item-${idx}`}
              role="option"
              aria-selected={idx === activeIndex}
              selected={idx === activeIndex}
              onMouseEnter={() => setActiveIndex(idx)}
              onClick={() => onSelect(item.path)}
            >
              <ListItemIcon>{item.icon}</ListItemIcon>
              <ListItemText primary={item.label} />
            </ListItemButton>
          ))
        )}
      </List>
    </Dialog>
  );
}
