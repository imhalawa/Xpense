import axios from "axios";
import { afterEach, describe, expect, it, vi } from "vitest";
import { encryptedSyncMutationApi } from "./encryptedVaultProjection";

const id = "11111111-1111-4111-8111-111111111111";
const ownerId = "22222222-2222-4222-8222-222222222222";
const wire = {
  id,
  recordType: 0,
  ownerUserId: ownerId,
  parentResourceId: id,
  revision: 1,
  protocolVersion: 1,
  nonce: "AQ==",
  ciphertext: "Ag==",
  envelopes: [{ id, groupId: null, wrappedKey: "Aw==", nonce: "BA==", encapsulatedKey: null, protocolVersion: 1 }],
  isDeleted: false,
  sequenceNumber: 1,
  createdAt: "2026-08-09T10:00:00Z",
  updatedAt: "2026-08-09T10:00:00Z",
};

afterEach(() => vi.restoreAllMocks());

describe("encrypted sync mutations", () => {
  it("refreshes antiforgery independently for create, replace, and delete", async () => {
    vi.spyOn(axios, "get")
      .mockResolvedValueOnce({ data: { requestToken: "create-token" } })
      .mockResolvedValueOnce({ data: { requestToken: "replace-token" } })
      .mockResolvedValueOnce({ data: { requestToken: "delete-token" } });
    const post = vi.spyOn(axios, "post").mockResolvedValue({ data: { records: [wire] } });
    const put = vi.spyOn(axios, "put").mockResolvedValue({ data: { ...wire, revision: 2 } });
    const remove = vi.spyOn(axios, "delete").mockResolvedValue({});
    const signal = new AbortController().signal;

    await encryptedSyncMutationApi.create({ records: [{ id, idempotencyKey: "33333333-3333-4333-8333-333333333333", recordType: 0, parentResourceId: id, protocolVersion: 1, nonce: new Uint8Array([1]), ciphertext: new Uint8Array([2]), personalEnvelope: { wrappedKey: new Uint8Array([3]), nonce: new Uint8Array([4]), protocolVersion: 1 } }] }, signal);
    await encryptedSyncMutationApi.replace(id, { expectedRevision: 1, protocolVersion: 1, nonce: new Uint8Array([5]), ciphertext: new Uint8Array([6]) }, signal);
    await encryptedSyncMutationApi.remove(id, signal);

    expect(axios.get).toHaveBeenCalledTimes(3);
    expect(post.mock.calls[0]![2]).toMatchObject({ headers: { "X-Xpense-Antiforgery": "create-token" } });
    expect(put.mock.calls[0]![2]).toMatchObject({ headers: { "X-Xpense-Antiforgery": "replace-token" } });
    expect(remove.mock.calls[0]![1]).toMatchObject({ headers: { "X-Xpense-Antiforgery": "delete-token" } });
  });
});
