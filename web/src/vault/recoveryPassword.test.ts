import { describe, expect, it } from "vitest";
import { deriveRecoveryKeyMaterial } from "../crypto/argon2";
import { createUserMasterKey } from "../crypto/keyHierarchy";
import { exportSymmetricKey } from "../crypto/primitives";
import type { MasterKeyWrapperDescriptor } from "../crypto/protocol";
import {
  configureRecoveryPassword,
  unwrapRecoveryPassword,
} from "./recoveryPassword";

const descriptor: MasterKeyWrapperDescriptor = {
  userId: "11111111-1111-4111-8111-111111111111",
  wrapperKind: "recoveryPassword",
  wrapperId: "22222222-2222-4222-8222-222222222222",
};
const password = "a memorable recovery password";
const olderParameters = {
  memoryKibibytes: 32,
  iterations: 2,
  lanes: 1,
  outputBytes: 32,
};

describe("recovery password wrapper", () => {
  it("wraps and restores the user master key", async () => {
    const masterKey = await createUserMasterKey();
    const wrapper = await configureRecoveryPassword(masterKey, password, descriptor, {
      parameters: olderParameters,
    });
    const restored = await unwrapRecoveryPassword(wrapper, password);

    expect(Array.from(await exportSymmetricKey(restored))).toEqual(
      Array.from(await exportSymmetricKey(masterKey)),
    );
  });

  it("rejects the wrong password", async () => {
    const wrapper = await configureRecoveryPassword(
      await createUserMasterKey(),
      password,
      descriptor,
      { parameters: olderParameters },
    );

    await expect(unwrapRecoveryPassword(wrapper, "a different wrong password")).rejects.toBeDefined();
  });

  it("stores its salt and full default parameter record", async () => {
    const wrapper = await configureRecoveryPassword(
      await createUserMasterKey(),
      password,
      descriptor,
    );

    expect(wrapper.salt).toHaveLength(16);
    expect(wrapper.parameters).toEqual({
      memoryKibibytes: 65_536,
      iterations: 3,
      lanes: 4,
      outputBytes: 32,
    });
  });

  it("continues to honour parameters stored by an older wrapper", async () => {
    const masterKey = await createUserMasterKey();
    const wrapper = await configureRecoveryPassword(masterKey, password, descriptor, {
      parameters: olderParameters,
    });

    await expect(unwrapRecoveryPassword(wrapper, password)).resolves.toBeDefined();
    expect(wrapper.parameters).toEqual(olderParameters);
  });

  it("refuses passwords under fourteen characters", async () => {
    await expect(
      configureRecoveryPassword(await createUserMasterKey(), "too short", descriptor),
    ).rejects.toThrow("The recovery password must contain at least 14 characters");
  });

  it("stores neither the password nor its derived key", async () => {
    const masterKey = await createUserMasterKey();
    const wrapper = await configureRecoveryPassword(masterKey, password, descriptor, {
      parameters: olderParameters,
    });
    const derivedKey = await deriveRecoveryKeyMaterial(
      password,
      wrapper.salt,
      wrapper.parameters,
    );
    const inspect = (value: unknown): void => {
      if (typeof value === "string") expect(value).not.toBe(password);
      if (value instanceof Uint8Array) {
        expect(Array.from(value)).not.toEqual(Array.from(derivedKey));
        return;
      }
      if (value !== null && typeof value === "object") {
        Object.values(value).forEach(inspect);
      }
    };

    inspect(wrapper);
  });
});
