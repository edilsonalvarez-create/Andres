import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { CssBaseline, ThemeProvider, createTheme } from "@mui/material";
import App from "./App";
import { AuthProvider } from "./auth/AuthContext";
import "./index.css";

const theme = createTheme({
  palette: {
    primary: { main: "#16213e" },
    secondary: { main: "#3f51b5" },
    background: { default: "#f4f6fb" },
  },
  typography: { fontFamily: '"Segoe UI", Roboto, Helvetica, Arial, sans-serif' },
});

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <AuthProvider>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </AuthProvider>
    </ThemeProvider>
  </React.StrictMode>
);
