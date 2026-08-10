import {
  openGroupKeyEnvelope,
  rotateGroupKey,
  wrapRecordKeyForGroup,
} from "../groupKeys";
import {
  createRecordKey,
  openRecordPayload,
  sealRecordPayload,
  unlockEncryptionIdentity,
  unwrapRecordKeyForOwner,
  wrapMasterKey,
  wrapRecordKeyForOwner,
} from "../keyHierarchy";
import type { HpkeEnvelope } from "../hpke";
import type {
  EncryptedRecordResult,
  GroupRotationResult,
  VaultWorkerCommand,
  VaultWorkerErrorCode,
  VaultWorkerResponse,
} from "./commands";

let userMasterKey: CryptoKey | null = null;
let identityPrivateKey: CryptoKey | null = null;
const groupKeys = new Map<string, CryptoKey>();
const groupEnvelopes = new Map<string, HpkeEnvelope>();
const recordKeys = new Map<string, CryptoKey>();

const successful = <T>(value: T): VaultWorkerResponse<T> => ({ ok: true, value });

const failed = (
  code: VaultWorkerErrorCode,
  message: string,
): VaultWorkerResponse<never> => ({ ok: false, error: { code, message } });

const locked = (): VaultWorkerResponse<never> => failed("vault-locked", "The vault is locked");

const clearKeys = (): void => {
  userMasterKey = null;
  identityPrivateKey = null;
  groupKeys.clear();
  groupEnvelopes.clear();
  recordKeys.clear();
};

export const handleVaultCommand = async (
  command: VaultWorkerCommand,
): Promise<VaultWorkerResponse> => {
  const commandType = (command as { type?: unknown }).type;
  if (
    typeof commandType !== "string" ||
    ![
      "unlockWithMasterKey",
      "lock",
      "encryptRecord",
      "encryptReplacement",
      "decryptRecord",
      "addGroupEnvelope",
      "removeGroupEnvelope",
      "importGroupKey",
      "rotateGroup",
      "wrapMasterKeyForNewWrapper",
    ].includes(commandType)
  ) {
    return failed("unknown-command", "The vault command is not supported");
  }

  try {
    switch (command.type) {
      case "unlockWithMasterKey": {
        const unlockedIdentity = command.encryptedPrivateKey
          ? await unlockEncryptionIdentity(
              command.masterKey,
              command.userId,
              command.encryptedPrivateKey,
            )
          : null;
        clearKeys();
        userMasterKey = command.masterKey;
        identityPrivateKey = unlockedIdentity;
        return successful({ unlocked: true });
      }
      case "lock":
        clearKeys();
        return successful({ locked: true });
      case "encryptRecord": {
        if (userMasterKey === null) return locked();
        const recordKey = await createRecordKey();
        const sealedPayload = await sealRecordPayload(
          recordKey,
          command.payload,
          command.payloadDescriptor,
        );
        const personalEnvelope = await wrapRecordKeyForOwner(
          userMasterKey,
          recordKey,
          command.personalEnvelopeDescriptor,
        );
        let groupEnvelope;
        if (command.groupEnvelopeDescriptor) {
          const groupId = command.groupEnvelopeDescriptor.groupId;
          const groupKey = groupId === null ? undefined : groupKeys.get(groupId);
          if (groupKey === undefined) {
            return failed("group-key-missing", "The group key is not available");
          }
          groupEnvelope = await wrapRecordKeyForGroup(
            groupKey,
            recordKey,
            command.groupEnvelopeDescriptor,
          );
        }
        recordKeys.set(command.payloadDescriptor.recordId, recordKey);
        const result: EncryptedRecordResult = {
          sealedPayload,
          personalEnvelope,
          groupEnvelope,
        };
        return successful(result);
      }
      case "encryptReplacement": {
        if (userMasterKey === null) return locked();
        const recordKey = recordKeys.get(command.payloadDescriptor.recordId);
        if (recordKey === undefined) {
          return failed("operation-failed", "The record key is not available");
        }
        return successful(
          await sealRecordPayload(recordKey, command.payload, command.payloadDescriptor),
        );
      }
      case "decryptRecord": {
        if (userMasterKey === null) return locked();
        let recordKey = recordKeys.get(command.payloadDescriptor.recordId);
        if (recordKey === undefined) {
          recordKey = await unwrapRecordKeyForOwner(
            userMasterKey,
            command.personalEnvelope,
            command.personalEnvelopeDescriptor,
          );
          recordKeys.set(command.payloadDescriptor.recordId, recordKey);
        }
        return successful(
          await openRecordPayload(recordKey, command.sealedPayload, command.payloadDescriptor),
        );
      }
      case "addGroupEnvelope":
        groupEnvelopes.set(command.groupId, command.envelope);
        return successful({ stored: true });
      case "removeGroupEnvelope":
        groupEnvelopes.delete(command.groupId);
        groupKeys.delete(command.groupId);
        return successful({ removed: true });
      case "importGroupKey": {
        if (identityPrivateKey === null) return locked();
        const envelope = groupEnvelopes.get(command.groupId);
        if (envelope === undefined) {
          return failed("group-key-missing", "The group envelope is not available");
        }
        const groupKey = await openGroupKeyEnvelope(
          identityPrivateKey,
          envelope,
          command.groupId,
        );
        groupKeys.set(command.groupId, groupKey);
        return successful({ imported: true });
      }
      case "rotateGroup": {
        if (userMasterKey === null) return locked();
        const rotation = await rotateGroupKey(
          command.previousMembers,
          command.remainingMembers,
          command.groupId,
        );
        groupKeys.set(command.groupId, rotation.groupKey);
        const result: GroupRotationResult = {
          memberEnvelopes: rotation.memberEnvelopes,
          removedMemberIds: rotation.removedMemberIds,
        };
        return successful(result);
      }
      case "wrapMasterKeyForNewWrapper":
        if (userMasterKey === null) return locked();
        return successful(
          await wrapMasterKey(command.wrappingKey, userMasterKey, command.descriptor),
        );
    }
  } catch {
    return failed("operation-failed", "The vault command could not be completed");
  }
};
