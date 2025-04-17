import React from "react";
import { Drive } from "./components/drive";
import ReactDOM from "react-dom/client";
function App() {
  return <Drive />;
}

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
