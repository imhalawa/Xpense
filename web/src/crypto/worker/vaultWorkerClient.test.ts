import { describe, expect, it, vi } from "vitest";
import type { VaultWorkerCommand, VaultWorkerResponse } from "./commands";
import {
  VaultWorkerClient,
  VaultWorkerUnavailableError,
  type VaultWorkerMessage,
  type VaultWorkerReply,
  type WorkerPort,
} from "./vaultWorkerClient";

class StubWorker implements WorkerPort {
  onmessage: ((event: MessageEvent<VaultWorkerReply>) => void) | null = null;
  onerror: ((event: ErrorEvent) => void) | null = null;
  readonly posted: VaultWorkerMessage[] = [];
  readonly terminate = vi.fn();

  postMessage(message: VaultWorkerMessage): void {
    this.posted.push(message);
  }

  reply(index: number, response: VaultWorkerResponse): void {
    const request = this.posted[index];
    if (request === undefined) throw new Error("The worker request does not exist");
    this.onmessage?.(
      new MessageEvent("message", { data: { requestId: request.requestId, response } }),
    );
  }
}

const lockCommand: VaultWorkerCommand = { type: "lock" };
const removeCommand: VaultWorkerCommand = {
  type: "removeGroupEnvelope",
  groupId: "11111111-1111-4111-8111-111111111111",
};

describe("vault worker client", () => {
  it("correlates concurrent responses by request id", async () => {
    const worker = new StubWorker();
    const client = new VaultWorkerClient(() => worker);
    const first = client.request(lockCommand);
    const second = client.request(removeCommand);

    worker.reply(1, { ok: true, value: { name: "second" } });
    worker.reply(0, { ok: true, value: { name: "first" } });

    await expect(first).resolves.toEqual({ ok: true, value: { name: "first" } });
    await expect(second).resolves.toEqual({ ok: true, value: { name: "second" } });
    expect(worker.posted[0].requestId).not.toBe(worker.posted[1].requestId);
  });

  it("terminates the worker and rejects every in-flight request", async () => {
    const worker = new StubWorker();
    const client = new VaultWorkerClient(() => worker);
    const first = client.request(lockCommand);
    const second = client.request(removeCommand);

    client.terminate();

    expect(worker.terminate).toHaveBeenCalledOnce();
    await expect(first).rejects.toBeInstanceOf(VaultWorkerUnavailableError);
    await expect(second).rejects.toBeInstanceOf(VaultWorkerUnavailableError);
    await expect(client.request(lockCommand)).rejects.toBeInstanceOf(VaultWorkerUnavailableError);
  });

  it("rejects pending work when the worker reports an error", async () => {
    const worker = new StubWorker();
    const client = new VaultWorkerClient(() => worker);
    const request = client.request(lockCommand);

    worker.onerror?.({ message: "worker crashed" } as ErrorEvent);

    await expect(request).rejects.toBeInstanceOf(VaultWorkerUnavailableError);
    expect(worker.terminate).toHaveBeenCalledOnce();
  });
});
