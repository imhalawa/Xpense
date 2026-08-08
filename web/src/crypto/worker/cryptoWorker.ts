import { handleVaultCommand } from "./handler";
import type {
  VaultWorkerMessage,
  VaultWorkerReply,
} from "./vaultWorkerClient";

interface VaultWorkerScope {
  onmessage: ((event: MessageEvent<VaultWorkerMessage>) => void) | null;
  postMessage(message: VaultWorkerReply): void;
}

const workerScope = self as unknown as VaultWorkerScope;

workerScope.onmessage = async (event) => {
  const response = await handleVaultCommand(event.data.command);
  workerScope.postMessage({ requestId: event.data.requestId, response });
};
