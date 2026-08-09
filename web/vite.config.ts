import { defineConfig } from "vite";
import react from "@vitejs/plugin-react-swc";

const contentSecurityPolicy = [
  "default-src 'self'",
  "script-src 'self'",
  "style-src 'self' 'unsafe-inline'",
  "connect-src 'self'",
  "worker-src 'self'",
  "object-src 'none'",
  "base-uri 'none'",
  "form-action 'self'",
  "frame-src 'none'",
  "trusted-types xpense-vault-worker",
  "require-trusted-types-for 'script'",
].join("; ");

export default defineConfig({
  plugins: [
    react(),
    {
      name: "xpense-content-security-policy",
      apply: "build",
      transformIndexHtml: {
        order: "pre",
        handler: () => [
          {
            tag: "meta",
            attrs: {
              "http-equiv": "Content-Security-Policy",
              content: contentSecurityPolicy,
            },
            injectTo: "head-prepend",
          },
        ],
      },
    },
  ],
  server: {
    proxy: {
      "/api": {
        target: "http://localhost:4000",
        changeOrigin: true,
      },
    },
  },
});
