import { describe, expect, it } from "vitest";
import config from "./vite.config";

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
