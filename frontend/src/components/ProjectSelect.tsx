import { useEffect, useState } from "react";
import { MenuItem, TextField } from "@mui/material";
import { api } from "../api/client";
import type { Paged, Project } from "../types";

interface Props {
  value: string;
  onChange: (projectId: string) => void;
}

/** Selector de proyecto reutilizable; selecciona el primero automáticamente. */
export default function ProjectSelect({ value, onChange }: Props) {
  const [projects, setProjects] = useState<Project[]>([]);

  useEffect(() => {
    api.get<Paged<Project>>("/projects", { params: { pageSize: 100 } }).then((r) => {
      setProjects(r.data.items);
      if (!value && r.data.items.length > 0) onChange(r.data.items[0].id);
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <TextField
      select
      size="small"
      label="Proyecto"
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="min-w-64"
    >
      {projects.map((p) => (
        <MenuItem key={p.id} value={p.id}>
          {p.code} — {p.name}
        </MenuItem>
      ))}
    </TextField>
  );
}
