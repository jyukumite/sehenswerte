// Entry point for the standalone harness. Everything visible comes from
// DevPlayground - this file only mounts it, and imports through the library's public
// barrel so a broken export shows up here too.
import React from "react";
import { createRoot } from "react-dom/client";

import { DevPlayground } from "../index";

const container = document.getElementById("root");
if (container === null) throw new Error("#root missing from index.html");

// StrictMode is deliberate: its double render/remount is how a mount-ordering bug in
// the controls surfaces here rather than in an embedding app. Drop it if it gets in
// the way of judging interaction feel.
createRoot(container).render(
  <React.StrictMode>
    <DevPlayground />
  </React.StrictMode>
);