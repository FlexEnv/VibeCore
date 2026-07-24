import "vite/modulepreload-polyfill";
import React from "react";
import ReactDOM from "react-dom/client";
import { ThemeProvider } from "./contexts/ThemeContext";
import { QueryProvider } from "./providers/QueryProvider";
import App from "./App";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <ThemeProvider>
      <QueryProvider>
        <App />
      </QueryProvider>
    </ThemeProvider>
  </React.StrictMode>,
);
