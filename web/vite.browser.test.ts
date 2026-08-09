import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import { chromium } from "playwright";
import { build, createServer, preview } from "vite";

const buildDirectories: string[] = [];

afterEach(async () => {
  await Promise.all(
    buildDirectories.splice(0).map((directory) => rm(directory, { force: true, recursive: true })),
  );
});

describe("browser security policy", () => {
  it("loads the vault worker under the production Trusted Types policy", async () => {
    const outputDirectory = await mkdtemp(join(tmpdir(), "xpense-web-browser-"));
    buildDirectories.push(outputDirectory);
    await build({
      configFile: join(import.meta.dirname, "vite.config.ts"),
      mode: "production",
      build: { outDir: outputDirectory },
      logLevel: "silent",
    });

    const previewServer = await preview({
      configFile: false,
      root: import.meta.dirname,
      build: { outDir: outputDirectory },
      preview: { host: "localhost", port: 0 },
      logLevel: "silent",
    });
    const browserServer = await chromium.launchServer({ headless: true });
    const browser = await chromium.connect(browserServer.wsEndpoint());
    try {
      const page = await browser.newPage();
      const pageErrors: string[] = [];
      const consoleErrors: string[] = [];
      const workerRequests: string[] = [];
      page.on("pageerror", (error) => pageErrors.push(error.message));
      page.on("console", (message) => {
        if (message.type() === "error") consoleErrors.push(message.text());
      });
      page.on("request", (request) => {
        if (/\/assets\/cryptoWorker-.*\.js$/.test(request.url())) workerRequests.push(request.url());
      });
      await page.addInitScript(() => {
        const target = window as typeof window & { xpenseCspViolations?: string[] };
        target.xpenseCspViolations = [];
        window.addEventListener("securitypolicyviolation", (event) => {
          target.xpenseCspViolations?.push(`${event.violatedDirective}:${event.blockedURI}`);
        });
      });

      const url = previewServer.resolvedUrls?.local[0];
      expect(url).toBeDefined();
      await page.goto(url!, { waitUntil: "domcontentloaded" });
      await page.waitForTimeout(1_000);
      const rendered = await page.locator("#root").evaluate((root) => root.childElementCount > 0);
      const violations = await page.evaluate(
        () =>
          (window as typeof window & { xpenseCspViolations?: string[] }).xpenseCspViolations ?? [],
      );
      const trustedTypesAvailable = await page.evaluate(
        () => typeof (window as typeof window & { trustedTypes?: unknown }).trustedTypes,
      );

      expect({
        rendered,
        workerLoaded: workerRequests.length > 0,
        violations,
        pageErrors,
        consoleErrors,
        trustedTypesAvailable,
      }).toEqual({
        rendered: true,
        workerLoaded: true,
        violations: [],
        pageErrors: [],
        consoleErrors: [],
        trustedTypesAvailable: "object",
      });
    } finally {
      await browserServer.kill();
      await previewServer.close();
    }
  }, 30_000);

  it("loads the source vault worker in test-mode browser servers", async () => {
    const developmentServer = await createServer({
      configFile: join(import.meta.dirname, "vite.config.ts"),
      mode: "test",
      server: { host: "localhost", port: 0 },
      logLevel: "silent",
    });
    await developmentServer.listen();
    const browserServer = await chromium.launchServer({ headless: true });
    const browser = await chromium.connect(browserServer.wsEndpoint());
    try {
      const page = await browser.newPage();
      const pageErrors: string[] = [];
      const workerRequests: string[] = [];
      page.on("pageerror", (error) => pageErrors.push(error.message));
      page.on("request", (request) => {
        if (request.url().includes("cryptoWorker.ts")) workerRequests.push(request.url());
      });

      const url = developmentServer.resolvedUrls?.local[0];
      expect(url).toBeDefined();
      await page.goto(url!, { waitUntil: "domcontentloaded" });
      await page.waitForTimeout(1_000);

      const rendered = await page.locator("#root").evaluate((root) => root.childElementCount > 0);
      expect({ rendered, workerLoaded: workerRequests.length > 0, pageErrors }).toEqual({
        rendered: true,
        workerLoaded: true,
        pageErrors: [],
      });
    } finally {
      await browserServer.kill();
      await developmentServer.close();
    }
  }, 30_000);
});
