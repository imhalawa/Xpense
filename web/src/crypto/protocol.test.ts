import { describe, expect, it } from "vitest";
import {
  PROTOCOL_VERSION,
  PASSKEY_WRAPPING_INFO,
  encryptionIdentityAdditionalData,
  envelopeAdditionalData,
  groupKeyAdditionalData,
  masterKeyAdditionalData,
  payloadAdditionalData,
} from "./protocol";

const recordId = "11111111-1111-4111-8111-111111111111";
const ownerId = "22222222-2222-4222-8222-222222222222";
const groupId = "33333333-3333-4333-8333-333333333333";
const wrapperId = "44444444-4444-4444-8444-444444444444";
const text = (bytes: Uint8Array) => new TextDecoder().decode(bytes);

describe("crypto protocol", () => {
  it("pins protocol version one", () => {
    expect(PROTOCOL_VERSION).toBe(1);
    expect(PASSKEY_WRAPPING_INFO).toBe("Xpense passkey vault wrap v1");
  });

  it("encodes payload additional data canonically", () => {
    expect(
      text(
        payloadAdditionalData({
          recordId,
          recordType: "transaction",
          ownerId,
          revision: 7,
        }),
      ),
    ).toBe(`v1|payload|${recordId}|transaction|${ownerId}|7`);
  });

  it("encodes group and personal envelopes canonically", () => {
    expect(text(envelopeAdditionalData({ recordId, ownerId, groupId }))).toBe(
      `v1|envelope|${recordId}|${ownerId}|${groupId}`,
    );
    expect(text(envelopeAdditionalData({ recordId, ownerId, groupId: null }))).toBe(
      `v1|envelope|${recordId}|${ownerId}|personal`,
    );
  });

  it("encodes master-key wrappers canonically", () => {
    expect(
      text(
        masterKeyAdditionalData({
          userId: ownerId,
          wrapperKind: "recoveryPassword",
          wrapperId,
        }),
      ),
    ).toBe(`v1|master|${ownerId}|recoveryPassword|${wrapperId}`);
  });

  it("encodes encrypted identities canonically", () => {
    expect(text(encryptionIdentityAdditionalData({ userId: ownerId }))).toBe(
      `v1|identity|${ownerId}|x25519-private`,
    );
  });

  it("encodes group keys canonically", () => {
    expect(text(groupKeyAdditionalData(groupId))).toBe(`v1|group-key|${groupId}`);
  });

  it("binds the payload revision", () => {
    const first = payloadAdditionalData({
      recordId,
      recordType: "transaction",
      ownerId,
      revision: 1,
    });
    const second = payloadAdditionalData({
      recordId,
      recordType: "transaction",
      ownerId,
      revision: 2,
    });
    expect(Array.from(first)).not.toEqual(Array.from(second));
  });

  it("normalises uppercase UUIDs", () => {
    expect(
      text(
        payloadAdditionalData({
          recordId: recordId.toUpperCase(),
          recordType: "account",
          ownerId: ownerId.toUpperCase(),
          revision: 1,
        }),
      ),
    ).toBe(`v1|payload|${recordId}|account|${ownerId}|1`);
  });

  it("rejects identifiers and revisions outside the canonical grammar", () => {
    expect(() =>
      envelopeAdditionalData({ recordId: "not-a-uuid", ownerId, groupId: null }),
    ).toThrow("valid UUID");
    expect(() =>
      payloadAdditionalData({ recordId, recordType: "tag", ownerId, revision: 0 }),
    ).toThrow("positive integer");
  });
});
