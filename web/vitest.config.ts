import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react-swc";

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    projects: [
      {
        extends: true,
        test: {
          name: "crypto",
          environment: "node",
          setupFiles: ["./vitest.crypto.setup.ts"],
          include: ["src/crypto/**/*.test.ts"],
        },
      },
      {
        extends: true,
        test: {
          name: "web",
          environment: "jsdom",
          setupFiles: ["./vitest.setup.ts"],
          include: ["src/**/*.test.{ts,tsx}", "vite.config.test.ts"],
          exclude: ["src/crypto/**/*.test.ts"],
        },
      },
    ],
  },
});
