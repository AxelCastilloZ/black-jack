import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import AppProviders from "./app/AppProviders";
import "./index.css"; // 👈 importante

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <AppProviders>
      <App />
    </AppProviders>
  </React.StrictMode>
);

