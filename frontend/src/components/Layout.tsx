import { useEffect, useState } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import {
  AppBar, Avatar, Box, Breadcrumbs, Button, Divider, Drawer, IconButton, Link, List,
  ListItemButton, ListItemIcon, ListItemText, Menu, MenuItem, Toolbar, Tooltip, Typography,
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import DashboardIcon from "@mui/icons-material/Dashboard";
import FolderIcon from "@mui/icons-material/Folder";
import AccountTreeIcon from "@mui/icons-material/AccountTree";
import ChecklistIcon from "@mui/icons-material/Checklist";
import PlayCircleIcon from "@mui/icons-material/PlayCircle";
import BugReportIcon from "@mui/icons-material/BugReport";
import HubIcon from "@mui/icons-material/Hub";
import PeopleIcon from "@mui/icons-material/People";
import LogoutIcon from "@mui/icons-material/Logout";
import LockResetIcon from "@mui/icons-material/LockReset";
import ShieldIcon from "@mui/icons-material/Shield";
import DescriptionIcon from "@mui/icons-material/Description";
import GppGoodIcon from "@mui/icons-material/GppGood";
import AccountTreeOutlinedIcon from "@mui/icons-material/AccountTreeOutlined";
import ApprovalIcon from "@mui/icons-material/Approval";
import NotificationsActiveIcon from "@mui/icons-material/NotificationsActive";
import HistoryEduIcon from "@mui/icons-material/HistoryEdu";
import SearchIcon from "@mui/icons-material/Search";
import LightModeIcon from "@mui/icons-material/LightMode";
import DarkModeIcon from "@mui/icons-material/DarkMode";
import SettingsBrightnessIcon from "@mui/icons-material/SettingsBrightness";
import { useAuth } from "../auth/AuthContext";
import { useThemeMode } from "../theme/ThemeModeContext";
import ChangePasswordDialog from "./ChangePasswordDialog";
import CommandPalette, { type CommandItem } from "./CommandPalette";

const drawerWidth = 240;
const railWidth = 64;

const getNavigation = (): CommandItem[] => [
  { label: "Dashboard", path: "/", icon: <DashboardIcon /> },
  { label: "Proyectos", path: "/proyectos", icon: <FolderIcon /> },
  { label: "Catálogo", path: "/catalogo", icon: <AccountTreeIcon /> },
  { label: "Casos de prueba", path: "/casos", icon: <ChecklistIcon /> },
  { label: "Ejecuciones", path: "/ejecuciones", icon: <PlayCircleIcon /> },
  { label: "Defectos", path: "/defectos", icon: <BugReportIcon /> },
  // "Quality Gates" faltaba en este menú pese a tener ruta propia desde Sprint 1
  // (UX-UI-Audit.md L-07): funcionalidad invisible sin conocer la URL de memoria.
  { label: "Quality Gates", path: "/quality-gates", icon: <GppGoodIcon /> },
  { label: "Trazabilidad", path: "/trazabilidad", icon: <AccountTreeOutlinedIcon /> },
  { label: "Aprobaciones", path: "/aprobaciones", icon: <ApprovalIcon /> },
  { label: "Notificaciones", path: "/notificaciones", icon: <NotificationsActiveIcon /> },
  { label: "Auditoría", path: "/auditoria", icon: <HistoryEduIcon /> },
  { label: "Integraciones", path: "/integraciones", icon: <HubIcon /> },
  { label: "Documentación", path: "/documentacion", icon: <DescriptionIcon /> },
];

const THEME_ICONS = { light: <LightModeIcon />, dark: <DarkModeIcon />, system: <SettingsBrightnessIcon /> };
const THEME_LABELS = { light: "Claro", dark: "Oscuro", system: "Sistema" };
const THEME_ORDER = ["light", "dark", "system"] as const;

export default function Layout() {
  const [open, setOpen] = useState(true);
  const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
  const [pwdOpen, setPwdOpen] = useState(false);
  const [paletteOpen, setPaletteOpen] = useState(false);
  const { auth, logout, hasRole } = useAuth();
  const { preference, setPreference } = useThemeMode();
  const navigate = useNavigate();
  const location = useLocation();

  const navigation = getNavigation();
  const nav = hasRole("Administrador")
    ? [...navigation, { label: "Usuarios", path: "/usuarios", icon: <PeopleIcon /> }]
    : navigation;

  const currentItem = nav.find((item) => item.path === location.pathname);

  // Ctrl+K / Cmd+K abre la paleta de comandos desde cualquier pantalla (Nielsen N7).
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setPaletteOpen(true);
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, []);

  const goTo = (path: string) => {
    navigate(path);
    setPaletteOpen(false);
  };

  const cycleThemeMode = () => {
    const idx = THEME_ORDER.indexOf(preference);
    setPreference(THEME_ORDER[(idx + 1) % THEME_ORDER.length]);
  };

  return (
    <Box className="flex min-h-screen">
      {/* WCAG 2.4.1 Bypass Blocks: visible solo al recibir foco (Tab desde el inicio de la página). */}
      <a href="#main-content" className="skip-link">Saltar al contenido</a>

      <AppBar position="fixed" sx={{ zIndex: (t) => t.zIndex.drawer + 1 }}>
        <Toolbar className="flex justify-between gap-4">
          <Box className="flex items-center gap-2">
            <IconButton color="inherit" edge="start" onClick={() => setOpen(!open)} aria-label="Alternar menú lateral">
              <MenuIcon />
            </IconButton>
            <ShieldIcon />
            <Typography variant="h6" noWrap>
              QA Guardian
            </Typography>
          </Box>

          <Button
            variant="outlined"
            color="inherit"
            size="small"
            startIcon={<SearchIcon fontSize="small" />}
            onClick={() => setPaletteOpen(true)}
            sx={{ borderColor: "rgba(255,255,255,0.4)", textTransform: "none", ml: 2 }}
          >
            Buscar…
            <Box component="span" sx={{
              ml: 1.5, px: 0.75, py: 0.1, fontSize: 11, borderRadius: 0.5,
              border: "1px solid rgba(255,255,255,0.4)",
            }}>
              Ctrl+K
            </Box>
          </Button>

          <Box className="flex items-center gap-1 ml-auto">
            <Tooltip title={`Tema: ${THEME_LABELS[preference]} (clic para cambiar)`}>
              <IconButton color="inherit" onClick={cycleThemeMode} aria-label="Cambiar tema claro/oscuro/sistema">
                {THEME_ICONS[preference]}
              </IconButton>
            </Tooltip>
            <Typography variant="body2" sx={{ ml: 1 }}>{auth?.fullName}</Typography>
            <Tooltip title="Cuenta">
              <IconButton onClick={(e) => setMenuAnchor(e.currentTarget)} sx={{ p: 0, ml: 1 }}>
                <Avatar sx={{ width: 32, height: 32, bgcolor: "secondary.main" }}>
                  {auth?.fullName?.[0] ?? "?"}
                </Avatar>
              </IconButton>
            </Tooltip>
            <Menu anchorEl={menuAnchor} open={!!menuAnchor} onClose={() => setMenuAnchor(null)}>
              <MenuItem onClick={() => { setMenuAnchor(null); setPwdOpen(true); }}>
                <ListItemIcon><LockResetIcon fontSize="small" /></ListItemIcon>
                Cambiar contraseña
              </MenuItem>
              <MenuItem onClick={() => { setMenuAnchor(null); void logout(); navigate("/login"); }}>
                <ListItemIcon><LogoutIcon fontSize="small" /></ListItemIcon>
                Cerrar sesión
              </MenuItem>
            </Menu>
          </Box>
        </Toolbar>
      </AppBar>

      <Drawer
        variant="persistent"
        open
        sx={{
          width: open ? drawerWidth : railWidth,
          flexShrink: 0,
          transition: (t) => t.transitions.create("width"),
          "& .MuiDrawer-paper": {
            width: open ? drawerWidth : railWidth,
            boxSizing: "border-box",
            overflowX: "hidden",
            transition: (t) => t.transitions.create("width"),
          },
        }}
      >
        <Toolbar />
        <Divider />
        <List>
          {nav.map((item) => (
            <Tooltip key={item.path} title={open ? "" : item.label} placement="right">
              <ListItemButton
                selected={location.pathname === item.path}
                onClick={() => navigate(item.path)}
                sx={{ justifyContent: open ? "flex-start" : "center", px: open ? 2 : 1.5 }}
              >
                <ListItemIcon sx={{ minWidth: open ? 40 : "auto" }}>{item.icon}</ListItemIcon>
                {open && <ListItemText primary={item.label} />}
              </ListItemButton>
            </Tooltip>
          ))}
        </List>
      </Drawer>

      <Box component="main" id="main-content" tabIndex={-1} className="flex-1 p-6 flex flex-col gap-3" sx={{ mt: 8 }}>
        <Breadcrumbs aria-label="Ruta de navegación">
          <Link
            component="button" underline="hover" color="inherit"
            onClick={() => navigate("/")} className="!text-sm"
          >
            Inicio
          </Link>
          {currentItem && currentItem.path !== "/" && (
            <Typography color="text.primary" variant="body2">{currentItem.label}</Typography>
          )}
        </Breadcrumbs>
        <Outlet />
      </Box>

      <ChangePasswordDialog open={pwdOpen} onClose={() => setPwdOpen(false)} />
      <CommandPalette open={paletteOpen} onClose={() => setPaletteOpen(false)} items={nav} onSelect={goTo} />
    </Box>
  );
}
