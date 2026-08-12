import axios from "axios";
import { createSyncRecords, deleteSyncRecord, type CreateSyncRecord } from "../sync/syncClient";
import type { VaultRecord } from "../vault/vaultDatabase";
import type { ClaimApi, ClaimCompletion } from "./claimFlow";

const antiforgeryPath = "/api/v1/auth/antiforgery";

interface AntiforgeryResponse {
  requestToken: string;
}

interface CurrentIdentityResponse {
  id: string;
}

interface ClaimStartResponse {
  claimToken: string;
  expiresAt: string;
}

const toVaultRecord = (record: Awaited<ReturnType<typeof createSyncRecords>>[number]): VaultRecord => ({
  ...record,
});

export class ClaimHttpApi implements ClaimApi {
  async currentUserId(signal?: AbortSignal): Promise<string> {
    return (await axios.get<CurrentIdentityResponse>(
      "/api/v1/auth/me",
      signal === undefined ? undefined : { signal },
    )).data.id;
  }

  async start(signal?: AbortSignal): Promise<ClaimStartResponse> {
    return (await axios.post<ClaimStartResponse>(
      "/api/v1/claim/start",
      null,
      await this.mutationConfig(signal),
    )).data;
  }

  async download(claimToken: string, signal?: AbortSignal): Promise<unknown> {
    return (await axios.post(
      "/api/v1/claim/dataset",
      { claimToken },
      await this.mutationConfig(signal),
    )).data;
  }

  async upload(record: CreateSyncRecord, signal?: AbortSignal): Promise<VaultRecord> {
    const token = await this.antiforgeryToken(signal);
    const [created] = await createSyncRecords({ records: [record] }, signal, token);
    if (created === undefined) throw new Error("The encrypted claim record acknowledgement is missing.");
    return toVaultRecord(created);
  }

  async remove(id: string, signal?: AbortSignal): Promise<void> {
    const token = await this.antiforgeryToken(signal);
    await deleteSyncRecord(id, signal, token);
  }

  async complete(claimToken: string, signal?: AbortSignal): Promise<ClaimCompletion> {
    return (await axios.post<ClaimCompletion>(
      "/api/v1/claim/complete",
      { claimToken },
      await this.mutationConfig(signal),
    )).data;
  }

  private async antiforgeryToken(signal: AbortSignal | undefined): Promise<string> {
    return (await axios.get<AntiforgeryResponse>(
      antiforgeryPath,
      signal === undefined ? undefined : { signal },
    )).data.requestToken;
  }

  private async mutationConfig(
    signal: AbortSignal | undefined,
  ): Promise<{ signal?: AbortSignal; headers: Record<string, string> }> {
    return {
      ...(signal === undefined ? {} : { signal }),
      headers: { "X-Xpense-Antiforgery": await this.antiforgeryToken(signal) },
    };
  }
}
