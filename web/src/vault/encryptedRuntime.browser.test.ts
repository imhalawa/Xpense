import { afterEach, describe, expect, it } from "vitest";
import { createUserMasterKey } from "../crypto/keyHierarchy";
import type { EncryptedRecordResult } from "../crypto/worker/commands";
import { VaultWorkerClient } from "../crypto/worker/vaultWorkerClient";

const ownerId = "11111111-1111-4111-8111-111111111111";
const recordId = "22222222-2222-4222-8222-222222222222";
const workers: VaultWorkerClient[] = [];
const successful = <T>(response: Awaited<ReturnType<VaultWorkerClient["request"]>>): T => {
  if (!response.ok) throw new Error(response.error.message);
  return response.value as T;
};

afterEach(() => {
  for (const worker of workers.splice(0)) worker.terminate();
});

describe("encrypted runtime Worker", () => {
  it("creates, reloads, updates, and decrypts with one durable personal envelope", async () => {
    const masterKey = await createUserMasterKey();
    const descriptor = { recordId, recordType: "account" as const, ownerId, revision: 1 };
    const envelopeDescriptor = { recordId, ownerId, groupId: null };
    const firstWorker = new VaultWorkerClient();
    workers.push(firstWorker);
    successful(await firstWorker.request({ type: "unlockWithMasterKey", masterKey, userId: ownerId }));
    const created = successful<EncryptedRecordResult>(await firstWorker.request({
      type: "encryptRecord",
      payload: new TextEncoder().encode('{"label":"Wallet"}'),
      payloadDescriptor: descriptor,
      personalEnvelopeDescriptor: envelopeDescriptor,
    }));
    firstWorker.terminate();
    workers.splice(workers.indexOf(firstWorker), 1);

    const reloadedWorker = new VaultWorkerClient();
    workers.push(reloadedWorker);
    successful(await reloadedWorker.request({ type: "unlockWithMasterKey", masterKey, userId: ownerId }));
    successful(await reloadedWorker.request({
      type: "decryptRecord",
      sealedPayload: created.sealedPayload,
      payloadDescriptor: descriptor,
      personalEnvelope: created.personalEnvelope,
      personalEnvelopeDescriptor: envelopeDescriptor,
    }));
    const replacementDescriptor = { ...descriptor, revision: 2 };
    const replacement = successful<EncryptedRecordResult["sealedPayload"]>(await reloadedWorker.request({
      type: "encryptReplacement",
      payload: new TextEncoder().encode('{"label":"Savings"}'),
      payloadDescriptor: replacementDescriptor,
    }));
    const decrypted = successful<Uint8Array>(await reloadedWorker.request({
      type: "decryptRecord",
      sealedPayload: replacement,
      payloadDescriptor: replacementDescriptor,
      personalEnvelope: created.personalEnvelope,
      personalEnvelopeDescriptor: envelopeDescriptor,
    }));

    expect(new TextDecoder().decode(decrypted)).toBe('{"label":"Savings"}');
  });
});
