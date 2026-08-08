import type { VaultWorkerCommand, VaultWorkerResponse } from "./commands";

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

const createWorker = (): WorkerPort =>
  new Worker(new URL("./cryptoWorker.ts", import.meta.url), { type: "module" });

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
