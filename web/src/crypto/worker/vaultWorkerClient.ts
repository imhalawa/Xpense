import type { VaultWorkerCommand, VaultWorkerResponse } from "./commands";
import cryptoWorkerUrl from "./cryptoWorker.ts?worker&url";

export interface VaultWorkerMessage {
  requestId: string;
  command: VaultWorkerCommand;
}

export interface VaultWorkerReply {
  requestId: string;
  response: VaultWorkerResponse;
}

export interface WorkerPort {
  onmessage: ((event: MessageEvent<VaultWorkerReply>) => void) | null;
  onerror: ((event: ErrorEvent) => void) | null;
  postMessage(message: VaultWorkerMessage): void;
  terminate(): void;
}

export type VaultWorkerFactory = () => WorkerPort;

interface PendingRequest {
  resolve: (response: VaultWorkerResponse) => void;
  reject: (reason: Error) => void;
}

interface TrustedTypesFactory {
  createPolicy(
    name: string,
    rules: { createScriptURL: (value: string) => string },
  ): TrustedScriptUrlPolicy;
}

interface TrustedScriptUrlPolicy {
  createScriptURL(value: string): unknown;
}

const workerPolicyName = "xpense-vault-worker";
const productionWorkerPath = /^\/assets\/cryptoWorker-[A-Za-z0-9_-]+\.js$/;
const trustedTypes = (globalThis as typeof globalThis & { trustedTypes?: TrustedTypesFactory })
  .trustedTypes;
let trustedWorkerPolicy: TrustedScriptUrlPolicy | undefined;

const trustedWorkerUrl = (workerUrl: URL): string | URL => {
  if (workerUrl.origin !== window.location.origin) {
    throw new TypeError("The vault worker URL must be a same-origin asset");
  }
  if (!productionWorkerPath.test(workerUrl.pathname) || trustedTypes === undefined) return workerUrl;
  trustedWorkerPolicy ??= trustedTypes.createPolicy(workerPolicyName, {
    createScriptURL: (value) => {
      const candidate = new URL(value, window.location.href);
      if (
        candidate.origin !== window.location.origin ||
        !productionWorkerPath.test(candidate.pathname)
      ) {
        throw new TypeError("The vault worker URL must be a bundled same-origin asset");
      }
      return candidate.href;
    },
  });
  return trustedWorkerPolicy.createScriptURL(workerUrl.href) as string;
};

const createWorker = (): WorkerPort => {
  const workerUrl = new URL(cryptoWorkerUrl, window.location.href);
  return new Worker(trustedWorkerUrl(workerUrl), { type: "module" });
};

export class VaultWorkerClient {
  private readonly worker: WorkerPort;
  private readonly pending = new Map<string, PendingRequest>();
  private nextRequestId = 1;
  private terminated = false;

  constructor(workerFactory: VaultWorkerFactory = createWorker) {
    this.worker = workerFactory();
    this.worker.onmessage = (event) => this.receive(event.data);
    this.worker.onerror = (event) => {
      this.terminated = true;
      this.worker.terminate();
      this.rejectPending(new Error(event.message || "The vault worker failed"));
    };
  }

  request<T = unknown>(command: VaultWorkerCommand): Promise<VaultWorkerResponse<T>> {
    if (this.terminated) {
      return Promise.reject(new Error("The vault worker was terminated"));
    }
    const requestId = String(this.nextRequestId++);
    return new Promise<VaultWorkerResponse<T>>((resolve, reject) => {
      this.pending.set(requestId, {
        resolve: resolve as (response: VaultWorkerResponse) => void,
        reject,
      });
      this.worker.postMessage({ requestId, command });
    });
  }

  terminate(): void {
    if (this.terminated) return;
    this.terminated = true;
    this.worker.terminate();
    this.rejectPending(new Error("The vault worker was terminated"));
  }

  private receive(reply: VaultWorkerReply): void {
    const pendingRequest = this.pending.get(reply.requestId);
    if (pendingRequest === undefined) return;
    this.pending.delete(reply.requestId);
    pendingRequest.resolve(reply.response);
  }

  private rejectPending(error: Error): void {
    for (const pendingRequest of this.pending.values()) {
      pendingRequest.reject(error);
    }
    this.pending.clear();
  }
}
