import { beforeEach, describe, expect, it } from "vitest";
import { createGroupKey, sealGroupKeyForMember } from "../groupKeys";
import { importEncryptionPublicKey } from "../hpke";
import {
  createEncryptionIdentity,
  createRecordKey,
  createUserMasterKey,
} from "../keyHierarchy";
import { exportSymmetricKey } from "../primitives";
import type { EnvelopeDescriptor, PayloadDescriptor } from "../protocol";
import type {
  EncryptedRecordResult,
  VaultWorkerCommand,
  VaultWorkerResponse,
} from "./commands";
import { handleVaultCommand } from "./handler";

const userId = "11111111-1111-4111-8111-111111111111";
const groupId = "22222222-2222-4222-8222-222222222222";
const memberId = "33333333-3333-4333-8333-333333333333";
const recordId = "44444444-4444-4444-8444-444444444444";
const wrapperId = "55555555-5555-4555-8555-555555555555";

const payloadDescriptor: PayloadDescriptor = {
  recordId,
  recordType: "transaction",
  ownerId: userId,
  revision: 1,
};
const personalEnvelopeDescriptor: EnvelopeDescriptor = {
  recordId,
  ownerId: userId,
  groupId: null,
};
const groupEnvelopeDescriptor: EnvelopeDescriptor = { recordId, ownerId: userId, groupId };

const successfulValue = <T>(response: VaultWorkerResponse): T => {
  expect(response.ok).toBe(true);
  if (!response.ok) throw new Error(response.error.message);
  return response.value as T;
};

beforeEach(async () => {
  await handleVaultCommand({ type: "lock" });
});

describe("vault worker command handler", () => {
  it("unlocks, encrypts, and decrypts a transaction", async () => {
    const masterKey = await createUserMasterKey();
    successfulValue(
      await handleVaultCommand({ type: "unlockWithMasterKey", masterKey, userId }),
    );
    const payload = new TextEncoder().encode('{"amount":4200}');
    const encrypted = successfulValue<EncryptedRecordResult>(
      await handleVaultCommand({
        type: "encryptRecord",
        payload,
        payloadDescriptor,
        personalEnvelopeDescriptor,
      }),
    );
    const decrypted = successfulValue<Uint8Array>(
      await handleVaultCommand({
        type: "decryptRecord",
        sealedPayload: encrypted.sealedPayload,
        payloadDescriptor,
        personalEnvelope: encrypted.personalEnvelope,
        personalEnvelopeDescriptor,
      }),
    );

    expect(Array.from(decrypted)).toEqual(Array.from(payload));
  });

  it("replaces ciphertext with the cached record key and original envelope", async () => {
    const masterKey = await createUserMasterKey();
    successfulValue(
      await handleVaultCommand({ type: "unlockWithMasterKey", masterKey, userId }),
    );
    const encrypted = successfulValue<EncryptedRecordResult>(
      await handleVaultCommand({
        type: "encryptRecord",
        payload: new TextEncoder().encode('{"amount":4200}'),
        payloadDescriptor,
        personalEnvelopeDescriptor,
      }),
    );
    const replacementDescriptor = { ...payloadDescriptor, revision: 2 };
    const replacement = successfulValue<EncryptedRecordResult["sealedPayload"]>(
      await handleVaultCommand({
        type: "encryptReplacement",
        payload: new TextEncoder().encode('{"amount":6300}'),
        payloadDescriptor: replacementDescriptor,
      }),
    );

    const decrypted = successfulValue<Uint8Array>(
      await handleVaultCommand({
        type: "decryptRecord",
        sealedPayload: replacement,
        payloadDescriptor: replacementDescriptor,
        personalEnvelope: encrypted.personalEnvelope,
        personalEnvelopeDescriptor,
      }),
    );

    expect(new TextDecoder().decode(decrypted)).toBe('{"amount":6300}');
  });

  it("rehydrates a record key before replacing after worker lock", async () => {
    const masterKey = await createUserMasterKey();
    successfulValue(
      await handleVaultCommand({ type: "unlockWithMasterKey", masterKey, userId }),
    );
    const encrypted = successfulValue<EncryptedRecordResult>(
      await handleVaultCommand({
        type: "encryptRecord",
        payload: new TextEncoder().encode('{"amount":4200}'),
        payloadDescriptor,
        personalEnvelopeDescriptor,
      }),
    );
    await handleVaultCommand({ type: "lock" });
    successfulValue(
      await handleVaultCommand({ type: "unlockWithMasterKey", masterKey, userId }),
    );
    successfulValue(
      await handleVaultCommand({
        type: "decryptRecord",
        sealedPayload: encrypted.sealedPayload,
        payloadDescriptor,
        personalEnvelope: encrypted.personalEnvelope,
        personalEnvelopeDescriptor,
      }),
    );

    const response = await handleVaultCommand({
      type: "encryptReplacement",
      payload: new TextEncoder().encode('{"amount":8400}'),
      payloadDescriptor: { ...payloadDescriptor, revision: 2 },
    });

    expect(response.ok).toBe(true);
  });

  it("refuses replacement before the record key is loaded", async () => {
    const masterKey = await createUserMasterKey();
    successfulValue(
      await handleVaultCommand({ type: "unlockWithMasterKey", masterKey, userId }),
    );

    const response = await handleVaultCommand({
      type: "encryptReplacement",
      payload: new TextEncoder().encode('{"amount":8400}'),
      payloadDescriptor: { ...payloadDescriptor, revision: 2 },
    });

    expect(response).toEqual({
      ok: false,
      error: {
        code: "operation-failed",
        message: "The record key is not available",
      },
    });
  });

  it("returns vault-locked after lock clears every key", async () => {
    const masterKey = await createUserMasterKey();
    await handleVaultCommand({ type: "unlockWithMasterKey", masterKey, userId });
    const payload = new TextEncoder().encode("private");
    const encrypted = successfulValue<EncryptedRecordResult>(
      await handleVaultCommand({
        type: "encryptRecord",
        payload,
        payloadDescriptor,
        personalEnvelopeDescriptor,
      }),
    );
    await handleVaultCommand({ type: "lock" });

    const response = await handleVaultCommand({
      type: "decryptRecord",
      sealedPayload: encrypted.sealedPayload,
      payloadDescriptor,
      personalEnvelope: encrypted.personalEnvelope,
      personalEnvelopeDescriptor,
    });

    expect(response).toEqual({
      ok: false,
      error: { code: "vault-locked", message: "The vault is locked" },
    });
  });

  it("never lets key material escape from any command response", async () => {
    const masterKey = await createUserMasterKey();
    const wrappingKey = await createRecordKey();
    const groupKey = await createGroupKey();
    const rawFixtureKeys = await Promise.all(
      [masterKey, wrappingKey, groupKey].map(exportSymmetricKey),
    );
    const identity = await createEncryptionIdentity(masterKey, userId);
    const publicKey = await importEncryptionPublicKey(identity.publicKeyBytes);
    const groupEnvelope = await sealGroupKeyForMember(publicKey, groupKey, groupId);
    const responses: VaultWorkerResponse[] = [];

    responses.push(
      await handleVaultCommand({
        type: "unlockWithMasterKey",
        masterKey,
        userId,
        encryptedPrivateKey: identity.encryptedPrivateKey,
      }),
    );
    responses.push(
      await handleVaultCommand({ type: "addGroupEnvelope", groupId, envelope: groupEnvelope }),
    );
    responses.push(await handleVaultCommand({ type: "importGroupKey", groupId }));
    const encryptedResponse = await handleVaultCommand({
      type: "encryptRecord",
      payload: new TextEncoder().encode('{"merchant":"Hidden"}'),
      payloadDescriptor,
      personalEnvelopeDescriptor,
      groupEnvelopeDescriptor,
    });
    responses.push(encryptedResponse);
    const encrypted = successfulValue<EncryptedRecordResult>(encryptedResponse);
    responses.push(
      await handleVaultCommand({
        type: "decryptRecord",
        sealedPayload: encrypted.sealedPayload,
        payloadDescriptor,
        personalEnvelope: encrypted.personalEnvelope,
        personalEnvelopeDescriptor,
      }),
    );
    responses.push(
      await handleVaultCommand({
        type: "rotateGroup",
        groupId,
        previousMembers: [memberId],
        remainingMembers: [{ memberId, publicKey }],
      }),
    );
    responses.push(
      await handleVaultCommand({
        type: "wrapMasterKeyForNewWrapper",
        wrappingKey,
        descriptor: { userId, wrapperKind: "recoveryFile", wrapperId },
      }),
    );
    responses.push(await handleVaultCommand({ type: "removeGroupEnvelope", groupId }));
    responses.push(await handleVaultCommand({ type: "lock" }));

    const keyConstructor = masterKey.constructor;
    const inspect = (value: unknown): void => {
      expect(value).not.toBeInstanceOf(keyConstructor);
      if (value instanceof Uint8Array) {
        for (const rawKey of rawFixtureKeys) {
          expect(Array.from(value)).not.toEqual(Array.from(rawKey));
        }
        return;
      }
      if (Array.isArray(value)) {
        value.forEach(inspect);
        return;
      }
      if (value !== null && typeof value === "object") {
        Object.values(value).forEach(inspect);
      }
    };
    responses.forEach(inspect);
  });

  it("returns an error result for an unknown command", async () => {
    const response = await handleVaultCommand(
      { type: "eraseTheServer" } as unknown as VaultWorkerCommand,
    );

    expect(response).toEqual({
      ok: false,
      error: { code: "unknown-command", message: "The vault command is not supported" },
    });
  });
});
