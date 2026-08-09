import type {
  GroupMemberPublicKey,
  GroupMemberEnvelope,
} from "../groupKeys";
import type { HpkeEnvelope } from "../hpke";
import type { SealedBytes } from "../primitives";
import type {
  EnvelopeDescriptor,
  MasterKeyWrapperDescriptor,
  PayloadDescriptor,
} from "../protocol";

export type VaultWorkerErrorCode =
  | "vault-locked"
  | "unknown-command"
  | "group-key-missing"
  | "operation-failed";

export type VaultWorkerResponse<T = unknown> =
  | { ok: true; value: T }
  | { ok: false; error: { code: VaultWorkerErrorCode; message: string } };

export interface UnlockWithMasterKeyCommand {
  type: "unlockWithMasterKey";
  masterKey: CryptoKey;
  userId: string;
  encryptedPrivateKey?: Uint8Array;
}

export interface LockCommand {
  type: "lock";
}

export interface EncryptRecordCommand {
  type: "encryptRecord";
  payload: Uint8Array;
  payloadDescriptor: PayloadDescriptor;
  personalEnvelopeDescriptor: EnvelopeDescriptor;
  groupEnvelopeDescriptor?: EnvelopeDescriptor;
}

export interface EncryptReplacementCommand {
  type: "encryptReplacement";
  payload: Uint8Array;
  payloadDescriptor: PayloadDescriptor;
}

export interface DecryptRecordCommand {
  type: "decryptRecord";
  sealedPayload: SealedBytes;
  payloadDescriptor: PayloadDescriptor;
  personalEnvelope: SealedBytes;
  personalEnvelopeDescriptor: EnvelopeDescriptor;
}

export interface AddGroupEnvelopeCommand {
  type: "addGroupEnvelope";
  groupId: string;
  envelope: HpkeEnvelope;
}

export interface RemoveGroupEnvelopeCommand {
  type: "removeGroupEnvelope";
  groupId: string;
}

export interface ImportGroupKeyCommand {
  type: "importGroupKey";
  groupId: string;
}

export interface RotateGroupCommand {
  type: "rotateGroup";
  groupId: string;
  previousMembers: readonly string[];
  remainingMembers: readonly GroupMemberPublicKey[];
}

export interface WrapMasterKeyForNewWrapperCommand {
  type: "wrapMasterKeyForNewWrapper";
  wrappingKey: CryptoKey;
  descriptor: MasterKeyWrapperDescriptor;
}

export type VaultWorkerCommand =
  | UnlockWithMasterKeyCommand
  | LockCommand
  | EncryptRecordCommand
  | EncryptReplacementCommand
  | DecryptRecordCommand
  | AddGroupEnvelopeCommand
  | RemoveGroupEnvelopeCommand
  | ImportGroupKeyCommand
  | RotateGroupCommand
  | WrapMasterKeyForNewWrapperCommand;

export interface EncryptedRecordResult {
  sealedPayload: SealedBytes;
  personalEnvelope: SealedBytes;
  groupEnvelope?: SealedBytes;
}

export interface GroupRotationResult {
  memberEnvelopes: GroupMemberEnvelope[];
  removedMemberIds: string[];
}
