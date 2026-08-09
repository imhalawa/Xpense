import axios from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { CreateSyncRecord } from "../sync/syncClient";
import { ClaimHttpApi } from "./claimApi";

vi.mock("axios");

const record: CreateSyncRecord = {
  id: "11111111-1111-4111-8111-111111111111",
  idempotencyKey: "legacy-claim-v1:11111111-1111-4111-8111-111111111111",
  recordType: 0,
  parentResourceId: "11111111-1111-4111-8111-111111111111",
  protocolVersion: 1,
  nonce: new Uint8Array([1, 2, 3]),
  ciphertext: new Uint8Array([4, 5, 6]),
  personalEnvelope: {
    wrappedKey: new Uint8Array([7, 8, 9]),
    nonce: new Uint8Array([10, 11, 12]),
    protocolVersion: 1,
  },
};

const wireRecord = {
  id: record.id,
  recordType: 0,
  ownerUserId: "22222222-2222-4222-8222-222222222222",
  parentResourceId: record.parentResourceId,
  revision: 1,
  protocolVersion: 1,
  nonce: "AQID",
  ciphertext: "BAUG",
  envelopes: [{
    id: "33333333-3333-4333-8333-333333333333",
    groupId: null,
    wrappedKey: "BwgJ",
    nonce: "CgsM",
    encapsulatedKey: null,
    protocolVersion: 1,
  }],
  isDeleted: false,
  sequenceNumber: 1,
  createdAt: "2026-08-09T10:00:00Z",
  updatedAt: "2026-08-09T10:00:00Z",
};

describe("ClaimHttpApi", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    let token = 0;
    vi.mocked(axios.get).mockImplementation(async (url) => {
      if (url === "/api/v1/auth/antiforgery") {
        token += 1;
        return { data: { requestToken: `fresh-${token}` } };
      }
      return { data: { id: "22222222-2222-4222-8222-222222222222" } };
    });
    vi.mocked(axios.post).mockImplementation(async (url) => {
      if (url === "/api/v1/claim/start") {
        return { data: { claimToken: "claim", expiresAt: "2026-08-09T10:30:00Z" } };
      }
      if (url === "/api/v1/sync/records") return { data: { records: [wireRecord] } };
      if (url === "/api/v1/claim/complete") {
        return { data: { recordCount: 1, counts: {}, manifestHash: "a".repeat(64), completedAt: "2026-08-09T10:00:00Z" } };
      }
      return { data: { protocolVersion: 1 } };
    });
    vi.mocked(axios.delete).mockResolvedValue({ data: undefined });
  });

  it("fetches a fresh paired antiforgery token before every claim and sync mutation", async () => {
    const api = new ClaimHttpApi();

    await api.start();
    await api.download("claim");
    await api.upload(record);
    await api.remove(record.id);
    await api.complete("claim");

    expect(vi.mocked(axios.get).mock.calls.filter(([url]) =>
      url === "/api/v1/auth/antiforgery")).toHaveLength(5);
    expect(vi.mocked(axios.post).mock.calls[0]![2]).toMatchObject({
      headers: { "X-Xpense-Antiforgery": "fresh-1" },
    });
    expect(vi.mocked(axios.post).mock.calls[1]![2]).toMatchObject({
      headers: { "X-Xpense-Antiforgery": "fresh-2" },
    });
    expect(vi.mocked(axios.post).mock.calls[2]![2]).toMatchObject({
      headers: { "X-Xpense-Antiforgery": "fresh-3" },
    });
    expect(vi.mocked(axios.delete).mock.calls[0]![1]).toMatchObject({
      headers: { "X-Xpense-Antiforgery": "fresh-4" },
    });
    expect(vi.mocked(axios.post).mock.calls[3]![2]).toMatchObject({
      headers: { "X-Xpense-Antiforgery": "fresh-5" },
    });
  });

  it("serializes ciphertext only and maps the sync acknowledgement", async () => {
    const created = await new ClaimHttpApi().upload(record);

    expect(vi.mocked(axios.post)).toHaveBeenCalledWith(
      "/api/v1/sync/records",
      { records: [expect.objectContaining({
        id: record.id,
        idempotencyKey: record.idempotencyKey,
        nonce: "AQID",
        ciphertext: "BAUG",
        personalEnvelope: { wrappedKey: "BwgJ", nonce: "CgsM", protocolVersion: 1 },
      })] },
      expect.objectContaining({ headers: { "X-Xpense-Antiforgery": "fresh-1" } }),
    );
    expect(created).toMatchObject({ id: record.id, recordType: "account", tombstone: false });
  });
});
