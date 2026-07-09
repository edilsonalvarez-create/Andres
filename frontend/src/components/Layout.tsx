import { useState } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import {
  AppBar, Avatar, Box, Divider, Drawer, IconButton, List, ListItemButton,
  ListItemIcon, ListItemText, Toolbar, Tooltip, Typography,
} from "@mui/material";
import MenuIcon from "@mui/icons-material/Menu";
import DashboardIcon from "@mui/icons-material/Dashboard";
import FolderIcon from "@mui/icons-material/Folder";
import AccountTreeIcon from "@mui/icons-material/AccountTree";
import ChecklistIcon from "@mui/icons-material/Checklist";
import PlayCircleIcon from "@mui/icons-material/PlayCircle";
import BugReportIcon from "@mui/icons-material/BugReport";
import HubIcon from "@mui/icons-material/Hub";
import LogoutIcon from "@mui/icons-material/Logout";
import ShieldIcon from "@mui/icons-material/Shield";
import { useAuth } from "../auth/AuthContext";

const drawerWidth = 240;

const navigation = [
  { label: "Dashboard", path: "/", icon: <DashboardIcon /> },
  { label: "Proyectos", path: "/proyectos", icon: <FolderIcon /> },
  { label: "Catálogo", path: "/catalogo", icon: <AccountTreeIcon /> },
  { label: "Casos de prueba", path: "/casos", icon: <ChecklistIcon /> },
  { label: "Ejecuciones", path: "/ejecuciones", icon: <PlayCircleIcon /> },
  { label: "Defectos", path: "/defectos", icon: <BugReportIcon /> },
  { label: "Integraciones", path: "/integraciones", icon: <HubIcon /> },
];

export default function Layout() {
  const [open, setOpen] = useState(true);
  const { auth, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <Box className="flex min-h-screen">
      <AppBar position="fixed" sx={{ zIndex: (t) => t.zIndex.drawer + 1 }}>
        <Toolbar className="flex justify-between">
          <Box className="flex items-center gap-2">
            <IconButton color="inherit" edge="start" onClick={() => setOpen(!open)}>
              <MenuIcon />
            </IconButton>
            <ShieldIcon />
            <Typography variant="h6" noWrap>
              QA Guardian
            </Typography>
          </Box>
          <Box className="flex items-center gap-3">
            <Typography variant="body2">{auth?.fullName}</Typography>
            <Avatar sx={{ width: 32, height: 32, bgcolor: "secondary.main" }}>
              {auth?.fullName?.[0] ?? "?"}
            </Avatar>
            <Tooltip title="Cerrar sesión">
              <IconButton color="inherit" onClick={() => { logout(); navigate("/login"); }}>
                <LogoutIcon />
              </IconButton>
            </Tooltip>
          </Box>
        </Toolbar>
      </AppBar>

      <Drawer
        variant="persistent"
        open={open}
        sx={{
          width: open ? drawerWidth : 0,
          flexShrink: 0,
          "& .MuiDrawer-paper": { width: drawerWidth, boxSizing: "border-box" },
        }}
      >
        <Toolbar />
        <Divider />
        <List>
          {navigation.map((item) => (
            <ListItemButton
              key={item.path}
              selected={location.pathname === item.path}
              onClick={() => navigate(item.path)}
            >
              <ListItemIcon>{item.icon}</ListItemIcon>
              <ListItemText primary={item.label} />
            </ListItemButton>
          ))}
        </List>
      </Drawer>

      <Box component="main" className="flex-1 p-6" sx={{ mt: 8 }}>
        <Outlet />
      </Box>
    </Box>
  );
}
