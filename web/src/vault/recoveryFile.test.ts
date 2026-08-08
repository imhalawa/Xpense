import { describe, expect, it } from "vitest";
import { unwrapMasterKey } from "../crypto/keyHierarchy";
import { createUserMasterKey } from "../crypto/keyHierarchy";
import { exportSymmetricKey } from "../crypto/primitives";
import type { MasterKeyWrapperDescriptor } from "../crypto/protocol";
import {
  createRecoveryFile,
  deriveRecoveryFileWrappingKey,
  parseRecoveryFile,
  replaceRecoveryFile,
  unwrapRecoveryFile,
} from "./recoveryFile";

const descriptor: MasterKeyWrapperDescriptor = {
  userId: "11111111-1111-4111-8111-111111111111",
  wrapperKind: "recoveryFile",
  wrapperId: "22222222-2222-4222-8222-222222222222",
};

const base64Url = (bytes: Uint8Array): string => {
  let binary = "";
  for (const value of bytes) binary += String.fromCharCode(value);
  return btoa(binary).replaceAll("+", "-").replaceAll("/", "_").replace(/=+$/u, "");
};

describe("recovery file", () => {
  it("creates independent authentication and vault secrets", async () => {
    const bundle = await createRecoveryFile(await createUserMasterKey(), descriptor);
    const parsed = await parseRecoveryFile(bundle.fileText);

    expect(parsed.authenticationToken).toHaveLength(32);
    expect(parsed.vaultSecret).toHaveLength(32);
    expect(Array.from(parsed.authenticationToken)).not.toEqual(Array.from(parsed.vaultSecret));
    expect(Object.keys(bundle.serverPayload)).toEqual(["authenticationTokenHash"]);
    expect(bundle.serverPayload.authenticationTokenHash).toBe(
      base64Url(
        new Uint8Array(await crypto.subtle.digest("SHA-256", parsed.authenticationToken)),
      ),
    );
    expect(JSON.stringify(bundle.serverPayload)).not.toContain(bundle.fileText);
  });

  it("puts no identity or financial information in the file", async () => {
    const bundle = await createRecoveryFile(await createUserMasterKey(), descriptor);

    expect(bundle.fileText).not.toContain("owner@example.test");
    expect(bundle.fileText).not.toContain("Private shop purchase");
  });

  it("unlocks with the vault secret but not the authentication token", async () => {
    const masterKey = await createUserMasterKey();
    const bundle = await createRecoveryFile(masterKey, descriptor);
    const restored = await unwrapRecoveryFile(bundle.wrapper, bundle.fileText);
    const parsed = await parseRecoveryFile(bundle.fileText);
    const tokenWrappingKey = await deriveRecoveryFileWrappingKey(parsed.authenticationToken);

    expect(Array.from(await exportSymmetricKey(restored))).toEqual(
      Array.from(await exportSymmetricKey(masterKey)),
    );
    await expect(
      unwrapMasterKey(
        tokenWrappingKey,
        { nonce: bundle.wrapper.nonce, ciphertext: bundle.wrapper.ciphertext },
        descriptor,
      ),
    ).rejects.toBeDefined();
  });

  it("rejects a corrupted line with a clear error", async () => {
    const bundle = await createRecoveryFile(await createUserMasterKey(), descriptor);
    const corrupted = bundle.fileText.replace("Vault secret: ", "Vault secret: x");

    await expect(parseRecoveryFile(corrupted)).rejects.toThrow(
      "The recovery file is corrupted",
    );
  });

  it("replaces both secrets and makes the old file unable to open the new wrapper", async () => {
    const masterKey = await createUserMasterKey();
    const first = await createRecoveryFile(masterKey, descriptor);
    const second = await replaceRecoveryFile(masterKey, descriptor);
    const firstParsed = await parseRecoveryFile(first.fileText);
    const secondParsed = await parseRecoveryFile(second.fileText);

    expect(Array.from(firstParsed.authenticationToken)).not.toEqual(
      Array.from(secondParsed.authenticationToken),
    );
    expect(Array.from(firstParsed.vaultSecret)).not.toEqual(Array.from(secondParsed.vaultSecret));
    await expect(unwrapRecoveryFile(second.wrapper, first.fileText)).rejects.toBeDefined();
    await expect(unwrapRecoveryFile(second.wrapper, second.fileText)).resolves.toBeDefined();
  });
});
