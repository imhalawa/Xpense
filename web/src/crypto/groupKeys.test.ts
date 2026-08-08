import { describe, expect, it } from "vitest";
import { generateEncryptionIdentity } from "./hpke";
import {
  createGroupKey,
  openGroupKeyEnvelope,
  rotateGroupKey,
  rotateRecordKeys,
  sealGroupKeyForMember,
  unwrapRecordKeyForGroup,
  wrapRecordKeyForGroup,
} from "./groupKeys";
import {
  createRecordKey,
  createUserMasterKey,
  openRecordPayload,
  sealRecordPayload,
  unwrapRecordKeyForOwner,
} from "./keyHierarchy";
import { exportSymmetricKey } from "./primitives";
import type { EnvelopeDescriptor, PayloadDescriptor } from "./protocol";

const groupId = "11111111-1111-4111-8111-111111111111";
const ownerId = "33333333-3333-4333-8333-333333333333";
const remainingMemberId = "44444444-4444-4444-8444-444444444444";
const removedMemberId = "55555555-5555-4555-8555-555555555555";
const recordId = "66666666-6666-4666-8666-666666666666";

describe("group key access", () => {
  it("lets group members open the group key and rejects a stranger", async () => {
    const firstMember = await generateEncryptionIdentity();
    const secondMember = await generateEncryptionIdentity();
    const stranger = await generateEncryptionIdentity();
    const groupKey = await createGroupKey();
    const firstEnvelope = await sealGroupKeyForMember(
      firstMember.publicKey,
      groupKey,
      groupId,
    );
    const secondEnvelope = await sealGroupKeyForMember(
      secondMember.publicKey,
      groupKey,
      groupId,
    );

    const firstOpened = await openGroupKeyEnvelope(
      firstMember.privateKey,
      firstEnvelope,
      groupId,
    );
    const secondOpened = await openGroupKeyEnvelope(
      secondMember.privateKey,
      secondEnvelope,
      groupId,
    );
    expect(Array.from(await exportSymmetricKey(firstOpened))).toEqual(
      Array.from(await exportSymmetricKey(secondOpened)),
    );
    await expect(
      openGroupKeyEnvelope(stranger.privateKey, firstEnvelope, groupId),
    ).rejects.toBeDefined();
  });

  it("keeps records isolated between groups", async () => {
    const firstGroupKey = await createGroupKey();
    const secondGroupKey = await createGroupKey();
    const recordKey = await createRecordKey();
    const descriptor: EnvelopeDescriptor = { recordId, ownerId, groupId };
    const envelope = await wrapRecordKeyForGroup(firstGroupKey, recordKey, descriptor);

    await expect(
      unwrapRecordKeyForGroup(secondGroupKey, envelope, descriptor),
    ).rejects.toBeDefined();
  });

  it("preserves old history while excluding a removed member from the new key", async () => {
    const removedMember = await generateEncryptionIdentity();
    const remainingMember = await generateEncryptionIdentity();
    const oldGroupKey = await createGroupKey();
    const removedOldEnvelope = await sealGroupKeyForMember(
      removedMember.publicKey,
      oldGroupKey,
      groupId,
    );
    const rotation = await rotateGroupKey(
      [removedMemberId, remainingMemberId],
      [{ memberId: remainingMemberId, publicKey: remainingMember.publicKey }],
      groupId,
    );
    const newRecordKey = await createRecordKey();
    const newRecordEnvelope = await wrapRecordKeyForGroup(rotation.groupKey, newRecordKey, {
      recordId,
      ownerId,
      groupId,
    });

    await expect(
      openGroupKeyEnvelope(removedMember.privateKey, removedOldEnvelope, groupId),
    ).resolves.toBeDefined();
    await expect(
      unwrapRecordKeyForGroup(oldGroupKey, newRecordEnvelope, { recordId, ownerId, groupId }),
    ).rejects.toBeDefined();
    expect(rotation.memberEnvelopes.map((envelope) => envelope.memberId)).toEqual([
      remainingMemberId,
    ]);
    await expect(
      openGroupKeyEnvelope(
        remainingMember.privateKey,
        rotation.memberEnvelopes[0].envelope,
        groupId,
      ),
    ).resolves.toBeDefined();
  });

  it("rotates record keys, payloads, and both access envelopes", async () => {
    const ownerMasterKey = await createUserMasterKey();
    const oldGroupKey = await createGroupKey();
    const newGroupKey = await createGroupKey();
    const oldRecordKey = await createRecordKey();
    const currentDescriptor: PayloadDescriptor = {
      recordId,
      recordType: "transaction",
      ownerId,
      revision: 1,
    };
    const nextDescriptor: PayloadDescriptor = { ...currentDescriptor, revision: 2 };
    const payload = new TextEncoder().encode('{"merchant":"Private shop"}');
    const sealedPayload = await sealRecordPayload(oldRecordKey, payload, currentDescriptor);
    const personalEnvelopeDescriptor: EnvelopeDescriptor = {
      recordId,
      ownerId,
      groupId: null,
    };
    const groupEnvelopeDescriptor: EnvelopeDescriptor = { recordId, ownerId, groupId };

    const [rotated] = await rotateRecordKeys(
      [
        {
          recordKey: oldRecordKey,
          sealedPayload,
          currentDescriptor,
          nextDescriptor,
          personalEnvelopeDescriptor,
          groupEnvelopeDescriptor,
        },
      ],
      newGroupKey,
      ownerMasterKey,
    );

    expect(Array.from(await exportSymmetricKey(rotated.recordKey))).not.toEqual(
      Array.from(await exportSymmetricKey(oldRecordKey)),
    );
    await expect(
      openRecordPayload(oldRecordKey, rotated.sealedPayload, nextDescriptor),
    ).rejects.toBeDefined();
    const personalRecordKey = await unwrapRecordKeyForOwner(
      ownerMasterKey,
      rotated.personalEnvelope,
      personalEnvelopeDescriptor,
    );
    await expect(
      openRecordPayload(personalRecordKey, rotated.sealedPayload, nextDescriptor),
    ).resolves.toEqual(payload);
    await expect(
      unwrapRecordKeyForGroup(oldGroupKey, rotated.groupEnvelope, groupEnvelopeDescriptor),
    ).rejects.toBeDefined();
    await expect(
      unwrapRecordKeyForGroup(newGroupKey, rotated.groupEnvelope, groupEnvelopeDescriptor),
    ).resolves.toBeDefined();
  });
});
