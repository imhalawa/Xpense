import {
  openWithPrivateKey,
  sealToPublicKey,
  type HpkeEnvelope,
} from "./hpke";
import {
  createRecordKey,
  openRecordPayload,
  sealRecordPayload,
  wrapRecordKeyForOwner,
} from "./keyHierarchy";
import {
  decryptWithAdditionalData,
  encryptWithAdditionalData,
  exportSymmetricKey,
  generateSymmetricKey,
  importSymmetricKey,
  type SealedBytes,
} from "./primitives";
import {
  envelopeAdditionalData,
  groupKeyAdditionalData,
  type EnvelopeDescriptor,
  type PayloadDescriptor,
} from "./protocol";

export interface GroupMemberPublicKey {
  memberId: string;
  publicKey: CryptoKey;
}

export interface GroupMemberEnvelope {
  memberId: string;
  envelope: HpkeEnvelope;
}

export interface GroupKeyRotation {
  groupKey: CryptoKey;
  memberEnvelopes: GroupMemberEnvelope[];
  removedMemberIds: string[];
}

export interface RotatableRecord {
  recordKey: CryptoKey;
  sealedPayload: SealedBytes;
  currentDescriptor: PayloadDescriptor;
  nextDescriptor: PayloadDescriptor;
  personalEnvelopeDescriptor: EnvelopeDescriptor;
  groupEnvelopeDescriptor: EnvelopeDescriptor;
}

export interface RotatedRecord {
  recordKey: CryptoKey;
  sealedPayload: SealedBytes;
  personalEnvelope: SealedBytes;
  groupEnvelope: SealedBytes;
  descriptor: PayloadDescriptor;
}

const requireGroupEnvelope = (descriptor: EnvelopeDescriptor): void => {
  if (descriptor.groupId === null) {
    throw new Error("The group envelope must identify a group");
  }
};

export const createGroupKey = (): Promise<CryptoKey> =>
  generateSymmetricKey({ extractable: true });

export const sealGroupKeyForMember = async (
  memberPublicKey: CryptoKey,
  groupKey: CryptoKey,
  groupId: string,
): Promise<HpkeEnvelope> => {
  const groupKeyBytes = await exportSymmetricKey(groupKey);
  try {
    return await sealToPublicKey(
      memberPublicKey,
      groupKeyBytes,
      groupKeyAdditionalData(groupId),
    );
  } finally {
    groupKeyBytes.fill(0);
  }
};

export const openGroupKeyEnvelope = async (
  memberPrivateKey: CryptoKey,
  envelope: HpkeEnvelope,
  groupId: string,
): Promise<CryptoKey> => {
  const groupKeyBytes = await openWithPrivateKey(
    memberPrivateKey,
    envelope.encapsulatedKey,
    envelope.ciphertext,
    groupKeyAdditionalData(groupId),
  );
  try {
    return await importSymmetricKey(groupKeyBytes, { extractable: true });
  } finally {
    groupKeyBytes.fill(0);
  }
};

export const wrapRecordKeyForGroup = async (
  groupKey: CryptoKey,
  recordKey: CryptoKey,
  descriptor: EnvelopeDescriptor,
): Promise<SealedBytes> => {
  requireGroupEnvelope(descriptor);
  const recordKeyBytes = await exportSymmetricKey(recordKey);
  try {
    return await encryptWithAdditionalData(
      groupKey,
      recordKeyBytes,
      envelopeAdditionalData(descriptor),
    );
  } finally {
    recordKeyBytes.fill(0);
  }
};

export const unwrapRecordKeyForGroup = async (
  groupKey: CryptoKey,
  envelope: SealedBytes,
  descriptor: EnvelopeDescriptor,
): Promise<CryptoKey> => {
  requireGroupEnvelope(descriptor);
  const recordKeyBytes = await decryptWithAdditionalData(
    groupKey,
    envelope.nonce,
    envelope.ciphertext,
    envelopeAdditionalData(descriptor),
  );
  try {
    return await importSymmetricKey(recordKeyBytes, { extractable: true });
  } finally {
    recordKeyBytes.fill(0);
  }
};

export const rotateGroupKey = async (
  previousMembers: readonly string[],
  remainingMemberPublicKeys: readonly GroupMemberPublicKey[],
  groupId: string,
): Promise<GroupKeyRotation> => {
  if (remainingMemberPublicKeys.length === 0) {
    throw new Error("The rotated group must keep at least one member");
  }
  const remainingMemberIds = new Set(
    remainingMemberPublicKeys.map((member) => member.memberId),
  );
  if (remainingMemberIds.size !== remainingMemberPublicKeys.length) {
    throw new Error("The rotated group cannot contain duplicate members");
  }
  const groupKey = await createGroupKey();
  const memberEnvelopes = await Promise.all(
    remainingMemberPublicKeys.map(async (member) => ({
      memberId: member.memberId,
      envelope: await sealGroupKeyForMember(member.publicKey, groupKey, groupId),
    })),
  );
  return {
    groupKey,
    memberEnvelopes,
    removedMemberIds: previousMembers.filter((memberId) => !remainingMemberIds.has(memberId)),
  };
};

export const rotateRecordKeys = async (
  records: readonly RotatableRecord[],
  newGroupKey: CryptoKey,
  ownerMasterKey: CryptoKey,
): Promise<RotatedRecord[]> => {
  const rotatedRecords: RotatedRecord[] = [];
  for (const record of records) {
    const payload = await openRecordPayload(
      record.recordKey,
      record.sealedPayload,
      record.currentDescriptor,
    );
    try {
      const recordKey = await createRecordKey();
      const sealedPayload = await sealRecordPayload(
        recordKey,
        payload,
        record.nextDescriptor,
      );
      const personalEnvelope = await wrapRecordKeyForOwner(
        ownerMasterKey,
        recordKey,
        record.personalEnvelopeDescriptor,
      );
      const groupEnvelope = await wrapRecordKeyForGroup(
        newGroupKey,
        recordKey,
        record.groupEnvelopeDescriptor,
      );
      rotatedRecords.push({
        recordKey,
        sealedPayload,
        personalEnvelope,
        groupEnvelope,
        descriptor: record.nextDescriptor,
      });
    } finally {
      payload.fill(0);
    }
  }
  return rotatedRecords;
};
