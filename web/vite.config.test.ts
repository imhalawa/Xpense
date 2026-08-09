import { mkdtemp, readFile, readdir, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, describe, expect, it } from "vitest";
import { build } from "vite";
import config from "./vite.config";

const buildDirectories: string[] = [];

afterEach(async () => {
  await Promise.all(buildDirectories.splice(0).map((directory) => rm(directory, { force: true, recursive: true })));
});

describe("development server", () => {
  it("proxies api requests without depending on the client port", () => {
    expect(config).toMatchObject({
      server: {
        proxy: {
          "/api": {
            target: "http://localhost:4000",
          },
        },
      },
    });
  });
});

describe("production browser policy", () => {
  it("builds a self-contained application with the strict script policy", async () => {
    const outputDirectory = await mkdtemp(join(tmpdir(), "xpense-web-build-"));
    buildDirectories.push(outputDirectory);

    await build({
      configFile: join(import.meta.dirname, "vite.config.ts"),
      mode: "production",
      build: { outDir: outputDirectory },
      logLevel: "silent",
    });

    const indexHtml = await readFile(join(outputDirectory, "index.html"), "utf8");
    const policies = [...indexHtml.matchAll(/<meta[^>]+http-equiv=["']Content-Security-Policy["'][^>]+content=["']([^"']+)["'][^>]*>/gi)];
    expect(policies).toHaveLength(1);

    const policy = policies[0][1].replaceAll("&#39;", "'");
    const directives = new Map(
      policy
        .split(";")
        .map((directive) => directive.trim().split(/\s+/))
        .filter(([name]) => name)
        .map(([name, ...values]) => [name, values]),
    );

    expect(directives).toEqual(
      new Map([
        ["default-src", ["'self'"]],
        ["script-src", ["'self'"]],
        ["style-src", ["'self'", "'unsafe-inline'"]],
        ["connect-src", ["'self'"]],
        ["worker-src", ["'self'"]],
        ["object-src", ["'none'"]],
        ["base-uri", ["'none'"]],
        ["form-action", ["'self'"]],
        ["frame-src", ["'none'"]],
        ["trusted-types", ["xpense-vault-worker"]],
        ["require-trusted-types-for", ["'script'"]],
      ]),
    );

    const scripts = [...indexHtml.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/gi)];
    expect(scripts.length).toBeGreaterThan(0);
    for (const [, attributes, body] of scripts) {
      const source = attributes.match(/\bsrc=["']([^"']+)["']/i)?.[1];
      expect(source).toMatch(/^\/(?!\/)/);
      expect(body.trim()).toBe("");
    }

    const modulePreloads = [...indexHtml.matchAll(/<link\b[^>]*rel=["']modulepreload["'][^>]*href=["']([^"']+)["'][^>]*>/gi)];
    for (const [, source] of modulePreloads) expect(source).toMatch(/^\/(?!\/)/);

    const assets = await readdir(join(outputDirectory, "assets"));
    const workerAsset = assets.find((asset) => /^cryptoWorker-.*\.js$/.test(asset));
    expect(workerAsset).toBeDefined();

    const javascript = await Promise.all(
      assets.filter((asset) => asset.endsWith(".js")).map((asset) => readFile(join(outputDirectory, "assets", asset), "utf8")),
    );
    expect(javascript.some((source) => source.includes(`/assets/${workerAsset}`))).toBe(true);
  });

});
