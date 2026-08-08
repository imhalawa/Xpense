export const PROTOCOL_VERSION = 1;

const canonicalUuidPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
const encoder = new TextEncoder();

export type RecordType =
  | "account"
  | "transaction"
  | "transfer"
  | "category"
  | "merchant"
  | "tag"
  | "budget"
  | "notification"
  | "userProfile"
  | "necessityScale";

export type WrapperKind = "passkey" | "recoveryPassword" | "recoveryFile";

export interface PayloadDescriptor {
  recordId: string;
  recordType: RecordType;
  ownerId: string;
  revision: number;
}

export interface EnvelopeDescriptor {
  recordId: string;
  ownerId: string;
  groupId: string | null;
}

export interface MasterKeyWrapperDescriptor {
  userId: string;
  wrapperKind: WrapperKind;
  wrapperId: string;
}

export interface EncryptionIdentityDescriptor {
  userId: string;
}

const canonicalUuid = (value: string): string => {
  const normalised = value.toLowerCase();
  if (!canonicalUuidPattern.test(normalised)) throw new Error("The identifier must be a valid UUID");
  return normalised;
};

export const payloadAdditionalData = (descriptor: PayloadDescriptor): Uint8Array => {
  if (!Number.isSafeInteger(descriptor.revision) || descriptor.revision < 1) {
    throw new Error("The revision must be a positive integer");
  }
  return encoder.encode(
    `v${PROTOCOL_VERSION}|payload|${canonicalUuid(descriptor.recordId)}|${descriptor.recordType}|${canonicalUuid(descriptor.ownerId)}|${descriptor.revision}`,
  );
};

export const envelopeAdditionalData = (descriptor: EnvelopeDescriptor): Uint8Array =>
  encoder.encode(
    `v${PROTOCOL_VERSION}|envelope|${canonicalUuid(descriptor.recordId)}|${canonicalUuid(descriptor.ownerId)}|${descriptor.groupId === null ? "personal" : canonicalUuid(descriptor.groupId)}`,
  );

export const masterKeyAdditionalData = (
  descriptor: MasterKeyWrapperDescriptor,
): Uint8Array =>
  encoder.encode(
    `v${PROTOCOL_VERSION}|master|${canonicalUuid(descriptor.userId)}|${descriptor.wrapperKind}|${canonicalUuid(descriptor.wrapperId)}`,
  );

export const encryptionIdentityAdditionalData = (
  descriptor: EncryptionIdentityDescriptor,
): Uint8Array =>
  encoder.encode(
    `v${PROTOCOL_VERSION}|identity|${canonicalUuid(descriptor.userId)}|x25519-private`,
  );

export const groupKeyAdditionalData = (groupId: string): Uint8Array =>
  encoder.encode(`v${PROTOCOL_VERSION}|group-key|${canonicalUuid(groupId)}`);
