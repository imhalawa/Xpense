import { describe, expect, it } from "vitest";
import {
  createEncryptionIdentity,
  createRecordKey,
  createUserMasterKey,
  openRecordPayload,
  sealRecordPayload,
  unlockEncryptionIdentity,
  unwrapMasterKey,
  unwrapRecordKeyForOwner,
  wrapMasterKey,
  wrapRecordKeyForOwner,
} from "./keyHierarchy";
import type {
  EnvelopeDescriptor,
  MasterKeyWrapperDescriptor,
  PayloadDescriptor,
} from "./protocol";

const ownerId = "11111111-1111-4111-8111-111111111111";
const otherOwnerId = "22222222-2222-4222-8222-222222222222";
const recordId = "33333333-3333-4333-8333-333333333333";

const payloadDescriptor: PayloadDescriptor = {
  recordId,
  recordType: "transaction",
  ownerId,
  revision: 1,
};
const envelopeDescriptor: EnvelopeDescriptor = { recordId, ownerId, groupId: null };

describe("vault key hierarchy", () => {
  it("round-trips a payload through its personal record-key envelope", async () => {
    const masterKey = await createUserMasterKey();
    const recordKey = await createRecordKey();
    const payload = new TextEncoder().encode('{"amount":1250,"merchant":"Bakery"}');
    const sealedPayload = await sealRecordPayload(recordKey, payload, payloadDescriptor);
    const recordEnvelope = await wrapRecordKeyForOwner(
      masterKey,
      recordKey,
      envelopeDescriptor,
    );

    const restoredRecordKey = await unwrapRecordKeyForOwner(
      masterKey,
      recordEnvelope,
      envelopeDescriptor,
    );
    const opened = await openRecordPayload(
      restoredRecordKey,
      sealedPayload,
      payloadDescriptor,
    );

    expect(Array.from(opened)).toEqual(Array.from(payload));
  });

  it("returns only bytes when creating an encrypted identity", async () => {
    const masterKey = await createUserMasterKey();
    const identity = await createEncryptionIdentity(masterKey, ownerId);

    expect(identity.publicKeyBytes).toBeInstanceOf(Uint8Array);
    expect(identity.encryptedPrivateKey).toBeInstanceOf(Uint8Array);
    expect(Object.values(identity).every((value) => value instanceof Uint8Array)).toBe(true);
    expect(Array.from(identity.encryptedPrivateKey)).not.toEqual(
      expect.arrayContaining(Array.from(identity.publicKeyBytes)),
    );
  });

  it("restores the identity private key as non-extractable", async () => {
    const masterKey = await createUserMasterKey();
    const identity = await createEncryptionIdentity(masterKey, ownerId);
    const privateKey = await unlockEncryptionIdentity(
      masterKey,
      ownerId,
      identity.encryptedPrivateKey,
    );

    expect(privateKey.extractable).toBe(false);
    await expect(crypto.subtle.exportKey("jwk", privateKey)).rejects.toBeDefined();
  });

  it("rejects another user's master key", async () => {
    const ownerMasterKey = await createUserMasterKey();
    const otherMasterKey = await createUserMasterKey();
    const recordKey = await createRecordKey();
    const recordEnvelope = await wrapRecordKeyForOwner(
      ownerMasterKey,
      recordKey,
      envelopeDescriptor,
    );

    await expect(
      unwrapRecordKeyForOwner(otherMasterKey, recordEnvelope, envelopeDescriptor),
    ).rejects.toBeDefined();
  });

  it("binds the record owner into both payload and envelope authentication", async () => {
    const masterKey = await createUserMasterKey();
    const recordKey = await createRecordKey();
    const sealedPayload = await sealRecordPayload(
      recordKey,
      new TextEncoder().encode("private"),
      payloadDescriptor,
    );
    const recordEnvelope = await wrapRecordKeyForOwner(
      masterKey,
      recordKey,
      envelopeDescriptor,
    );

    await expect(
      openRecordPayload(recordKey, sealedPayload, {
        ...payloadDescriptor,
        ownerId: otherOwnerId,
      }),
    ).rejects.toBeDefined();
    await expect(
      unwrapRecordKeyForOwner(masterKey, recordEnvelope, {
        ...envelopeDescriptor,
        ownerId: otherOwnerId,
      }),
    ).rejects.toBeDefined();
  });

  it("wraps and restores a user master key", async () => {
    const userMasterKey = await createUserMasterKey();
    const wrappingKey = await createRecordKey();
    const descriptor: MasterKeyWrapperDescriptor = {
      userId: ownerId,
      wrapperKind: "recoveryFile",
      wrapperId: "44444444-4444-4444-8444-444444444444",
    };
    const wrapper = await wrapMasterKey(wrappingKey, userMasterKey, descriptor);
    const restoredMasterKey = await unwrapMasterKey(wrappingKey, wrapper, descriptor);
    const recordKey = await createRecordKey();
    const envelope = await wrapRecordKeyForOwner(
      userMasterKey,
      recordKey,
      envelopeDescriptor,
    );

    await expect(
      unwrapRecordKeyForOwner(restoredMasterKey, envelope, envelopeDescriptor),
    ).resolves.toBeDefined();
  });
});
