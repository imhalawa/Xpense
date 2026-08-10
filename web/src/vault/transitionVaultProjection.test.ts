import { describe, expect, it, vi } from "vitest";
import { fixtureProjection } from "./fixtureProjection";
import {
  transitionVaultProjection,
  type EncryptedProjection,
  type ProjectionCryptoBridge,
} from "./transitionVaultProjection";

const empty = () => fixtureProjection({
  spaces: [{ id: "personal", name: "Personal", kind: "personal", canEdit: true }],
  accounts: { personal: [] },
  taxonomy: { personal: [] },
  transactions: { personal: [] },
});

const encrypted = (unlock = vi.fn().mockResolvedValue(undefined)): EncryptedProjection => {
  const projection = empty() as EncryptedProjection;
  projection.attachCrypto = vi.fn();
  projection.unlock = unlock;
  return projection;
};

describe("transition vault projection", () => {
  it("uses legacy only when the authenticated server explicitly reports legacy", async () => {
    const legacy = empty();
    const unlock = vi.spyOn(legacy, "unlock");
    const encryptedProjection = encrypted();
    const projection = transitionVaultProjection({
      legacy,
      encrypted: encryptedProjection,
      status: vi.fn().mockResolvedValue("legacy"),
    });

    await projection.unlock();

    expect(projection.dataMode).toBe("legacy");
    expect(unlock).toHaveBeenCalledOnce();
    expect(encryptedProjection.unlock).not.toHaveBeenCalled();
  });

  it("never falls back to legacy after encrypted mode is known and encrypted unlock fails", async () => {
    const legacy = empty();
    const legacyUnlock = vi.spyOn(legacy, "unlock");
    const encryptedUnlock = vi.fn().mockRejectedValue(new Error("bad ciphertext"));
    const projection = transitionVaultProjection({
      legacy,
      encrypted: encrypted(encryptedUnlock),
      status: vi.fn().mockResolvedValue("encrypted"),
    });

    await expect(projection.unlock()).rejects.toThrow("bad ciphertext");
    await expect(projection.unlock()).rejects.toThrow("bad ciphertext");

    expect(projection.dataMode).toBe("encrypted");
    expect(legacyUnlock).not.toHaveBeenCalled();
    expect(encryptedUnlock).toHaveBeenCalledTimes(2);
  });

  it("attaches Worker crypto only to the encrypted runtime before unlock", async () => {
    const encryptedProjection = encrypted();
    const projection = transitionVaultProjection({
      legacy: empty(),
      encrypted: encryptedProjection,
      status: vi.fn().mockResolvedValue("encrypted"),
    });
    const bridge = { ownerId: crypto.randomUUID() } as ProjectionCryptoBridge;

    projection.attachCrypto(bridge);
    await projection.unlock();

    expect(encryptedProjection.attachCrypto).toHaveBeenCalledWith(bridge);
    expect(encryptedProjection.unlock).toHaveBeenCalledOnce();
  });

  it("routes claiming mode to maintenance without touching either data source", async () => {
    const legacy = empty();
    const encryptedProjection = encrypted();
    const projection = transitionVaultProjection({
      legacy,
      encrypted: encryptedProjection,
      status: vi.fn().mockResolvedValue("claiming"),
    });

    await expect(projection.unlock()).resolves.toBeUndefined();

    expect(projection.dataMode).toBe("claiming");
    expect(projection.state).toBe("locked");
    expect(encryptedProjection.unlock).not.toHaveBeenCalled();
  });

  it.each([
    vi.fn().mockResolvedValue("unknown"),
    vi.fn().mockRejectedValue(new Error("offline")),
  ])("fails closed when server mode cannot be established", async (status) => {
    const legacy = empty();
    const legacyUnlock = vi.spyOn(legacy, "unlock");
    const projection = transitionVaultProjection({
      legacy,
      encrypted: encrypted(),
      status,
    });

    await expect(projection.unlock()).rejects.toThrow();

    expect(projection.dataMode).toBe("unknown");
    expect(legacyUnlock).not.toHaveBeenCalled();
  });
});
