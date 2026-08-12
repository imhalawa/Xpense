import axios from "axios";
import { afterEach, describe, expect, it, vi } from "vitest";
import { localSyncMutationApi } from "./localVaultProjection";

const id = "11111111-1111-4111-8111-111111111111";
const ownerId = "22222222-2222-4222-8222-222222222222";
const wire = {
  id,
  recordType: 0,
  ownerId,
  parentResourceId: id,
  revision: 1,
  payload: "Ag==",
  tombstone: false,
  sequenceNumber: 1,
  serverCreatedAt: "2026-08-09T10:00:00Z",
  serverUpdatedAt: "2026-08-09T10:00:00Z",
};

afterEach(() => vi.restoreAllMocks());

describe("local sync mutations", () => {
  it("refreshes antiforgery independently for create, replace, and delete", async () => {
    vi.spyOn(axios, "get")
      .mockResolvedValueOnce({ data: { requestToken: "create-token" } })
      .mockResolvedValueOnce({ data: { requestToken: "replace-token" } })
      .mockResolvedValueOnce({ data: { requestToken: "delete-token" } });
    const post = vi.spyOn(axios, "post").mockResolvedValue({ data: { records: [wire] } });
    const put = vi.spyOn(axios, "put").mockResolvedValue({ data: { ...wire, revision: 2 } });
    const remove = vi.spyOn(axios, "delete").mockResolvedValue({});
    const signal = new AbortController().signal;

    await localSyncMutationApi.create({ records: [{ id, idempotencyKey: "33333333-3333-4333-8333-333333333333", recordType: 0, parentResourceId: id, payload: new Uint8Array([2]) }] }, signal);
    await localSyncMutationApi.replace(id, { expectedRevision: 1, payload: new Uint8Array([6]) }, signal);
    await localSyncMutationApi.remove(id, signal);

    expect(axios.get).toHaveBeenCalledTimes(3);
    expect(post.mock.calls[0]![2]).toMatchObject({ headers: { "X-Xpense-Antiforgery": "create-token" } });
    expect(put.mock.calls[0]![2]).toMatchObject({ headers: { "X-Xpense-Antiforgery": "replace-token" } });
    expect(remove.mock.calls[0]![1]).toMatchObject({ headers: { "X-Xpense-Antiforgery": "delete-token" } });
  });
});
