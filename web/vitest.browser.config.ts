import { defineConfig } from "vitest/config";
import { playwright } from "@vitest/browser-playwright";

export default defineConfig({
  test: {
    projects: [
      {
        test: {
          name: "csp-runtime",
          environment: "node",
          include: ["vite.browser.test.ts"],
          fileParallelism: false,
        },
      },
      {
        optimizeDeps: {
          include: ["@hpke/core", "axios"],
        },
        test: {
          name: "vault-browser",
          include: ["src/vault/*.browser.test.ts"],
          fileParallelism: false,
          testTimeout: 30_000,
          browser: {
            enabled: true,
            provider: playwright(),
            instances: [{ browser: "chromium" }],
            headless: true,
            api: { host: "localhost" },
          },
        },
      },
    ],
  },
});
