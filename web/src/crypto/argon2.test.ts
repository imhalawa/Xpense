import { describe, expect, it } from "vitest";
import {
  RECOVERY_PASSWORD_PARAMETERS,
  deriveArgon2idKeyMaterial,
  deriveRecoveryKeyMaterial,
} from "./argon2";

const toHex = (bytes: Uint8Array): string =>
  Array.from(bytes, (value) => value.toString(16).padStart(2, "0")).join("");

describe("Argon2id recovery derivation", () => {
  it("matches the RFC 9106 Argon2id test vector", async () => {
    const result = await deriveArgon2idKeyMaterial(
      new Uint8Array(32).fill(1),
      new Uint8Array(16).fill(2),
      { memoryKibibytes: 32, iterations: 3, lanes: 4, outputBytes: 32 },
      {
        secret: new Uint8Array(8).fill(3),
        associatedData: new Uint8Array(12).fill(4),
      },
    );

    expect(toHex(result)).toBe(
      "0d640df58d78766c08c037a34a8b53c9d01ef0452d75b65eb52520e96b01e659",
    );
  });

  it("exports the fixed recovery parameters", () => {
    expect(RECOVERY_PASSWORD_PARAMETERS).toEqual({
      memoryKibibytes: 65_536,
      iterations: 3,
      lanes: 4,
      outputBytes: 32,
    });
    expect(Object.isFrozen(RECOVERY_PASSWORD_PARAMETERS)).toBe(true);
  });

  it("honours parameters stored with an existing wrapper", async () => {
    const salt = new Uint8Array(16).fill(7);
    const storedParameters = {
      memoryKibibytes: 32,
      iterations: 2,
      lanes: 1,
      outputBytes: 32,
    };

    const first = await deriveRecoveryKeyMaterial(
      "a sufficiently long password",
      salt,
      storedParameters,
    );
    const second = await deriveArgon2idKeyMaterial(
      new TextEncoder().encode("a sufficiently long password"),
      salt,
      storedParameters,
    );

    expect(Array.from(first)).toEqual(Array.from(second));
  });

  it("rejects a recovery password shorter than fourteen characters", async () => {
    await expect(
      deriveRecoveryKeyMaterial(
        "too short",
        new Uint8Array(16),
        RECOVERY_PASSWORD_PARAMETERS,
      ),
    ).rejects.toThrow("The recovery password must contain at least 14 characters");
  });
});
