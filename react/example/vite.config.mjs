// Plain .mjs and not .ts on purpose: in a submodule checkout this folder sits inside
// the embedding app's own source tree, and that app's `tsc --noEmit` sweeps it. A .ts
// config importing "vite" would fail there, where vite is not installed.
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  // root is this folder; the library it exercises lives one level up, so dev-server
  // file serving has to be allowed to reach out of the root (default is root only).
  server: {
    port: 5174,
    open: true,
    fs: { allow: [".."] },
  },
  plugins: [react()],
});