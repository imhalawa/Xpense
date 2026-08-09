import { describe, expect, it } from "vitest";
import { buildClaimManifest, hashClaimManifest } from "./manifest";

describe("legacy claim manifest", () => {
  it("matches the server known answers and is independent of input order", async () => {
    const first = [
      { recordType: "transaction", id: "00000000-0000-4000-8000-000000000002" },
      { recordType: "account", id: "00000000-0000-4000-8000-000000000001" },
    ];
    const second = [...first].reverse();

    expect(buildClaimManifest(first)).toBe(
      "2\naccount|00000000-0000-4000-8000-000000000001\ntransaction|00000000-0000-4000-8000-000000000002",
    );
    expect(buildClaimManifest(second)).toBe(buildClaimManifest(first));
    await expect(hashClaimManifest(first)).resolves.toBe(
      "66ae9df94a3b6925899f4cd8eb4c64120d772d859bcadb96f92b7dace698e755",
    );
    await expect(hashClaimManifest([])).resolves.toBe(
      "5feceb66ffc86f38d952786c6d696c79c2dbc239dd4e91b46729d73a27fb57e9",
    );
  });

  it("changes when one identity is omitted", async () => {
    const identities = [
      { recordType: "account", id: "00000000-0000-0000-0000-000000000001" },
      { recordType: "tag", id: "00000000-0000-0000-0000-000000000002" },
    ];

    expect(await hashClaimManifest(identities)).not.toBe(
      await hashClaimManifest(identities.slice(0, 1)),
    );
  });
});
