import axios from "axios";
import { afterEach, describe, expect, it } from "vitest";
import { cdp } from "vitest/browser";
import {
  createUserMasterKey,
  unwrapMasterKey,
  wrapMasterKey,
} from "../crypto/keyHierarchy";
import { exportSymmetricKey } from "../crypto/primitives";
import type { EnvelopeDescriptor, PayloadDescriptor } from "../crypto/protocol";
import type { EncryptedRecordResult } from "../crypto/worker/commands";
import { VaultWorkerClient } from "../crypto/worker/vaultWorkerClient";
import { createSyncRecords, SyncClient } from "../sync/syncClient";
import {
  deleteVaultDatabase,
  openVaultDatabase,
  type VaultDatabase,
  type VaultRecord,
} from "./vaultDatabase";
import { derivePasskeyWrappingKey } from "./passkey";

interface AttachedTarget {
  sessionId: string;
  targetInfo: { type: string };
}

interface TargetMessage {
  sessionId: string;
  message: string;
}

interface ProtocolReply {
  id?: number;
  result?: unknown;
  error?: { message: string };
}

interface BrowserCdpSession {
  send(method: string, params?: object): Promise<unknown>;
  on<T>(event: string, listener: (event: T) => void): void;
}

interface PausedRequest {
  requestId: string;
  request: {
    url: string;
    method: string;
    headers: Record<string, string>;
    postData?: string;
  };
}

interface NetworkRequest {
  request: {
    url: string;
    method: string;
    headers: Record<string, string>;
    postData?: string;
  };
}

interface CapturedRequest {
  url: string;
  method: string;
  headers: Record<string, string>;
  postData: string | null;
}

interface PrfResults {
  prf?: { results?: { first?: ArrayBuffer } };
}

interface Observation {
  path: string;
  text?: string;
  bytes?: Uint8Array<ArrayBuffer>;
  opaqueCryptoKey?: boolean;
}

type JsonResponse = Record<string, unknown>;

const authenticators: string[] = [];
const workers: VaultWorkerClient[] = [];
const databases: VaultDatabase[] = [];
const pageConsoleRestores: Array<() => void> = [];
let fetchInterceptionEnabled = false;
let networkCaptureEnabled = false;
let fetchListenerInstalled = false;
let activeCapturedRequests: CapturedRequest[] | null = null;
let activeResponder: ((request: CapturedRequest) => JsonResponse) | null = null;
let nextProtocolCommandId = 1;

const browserCdp = (): BrowserCdpSession => cdp() as unknown as BrowserCdpSession;

const randomBytes = (length: number): Uint8Array<ArrayBuffer> =>
  crypto.getRandomValues(new Uint8Array(length));

const toBase64 = (bytes: Uint8Array): string => {
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary);
};

const responseBody = (value: JsonResponse): string =>
  toBase64(new TextEncoder().encode(JSON.stringify(value)));

const addAuthenticator = async (): Promise<void> => {
  await browserCdp().send("WebAuthn.enable");
  const result = await browserCdp().send("WebAuthn.addVirtualAuthenticator", {
    options: {
      protocol: "ctap2",
      ctap2Version: "ctap2_1",
      transport: "internal",
      hasResidentKey: true,
      hasUserVerification: true,
      isUserVerified: true,
      automaticPresenceSimulation: true,
      hasPrf: true,
    },
  }) as { authenticatorId: string };
  authenticators.push(result.authenticatorId);
};

const realPrf = async (): Promise<Uint8Array<ArrayBuffer>> => {
  await addAuthenticator();
  const salt = randomBytes(32);
  const credential = await navigator.credentials.create({
    publicKey: {
      challenge: randomBytes(32),
      rp: { id: "localhost", name: "Xpense" },
      user: {
        id: randomBytes(16),
        name: "privacy-gate@example.test",
        displayName: "Privacy gate",
      },
      pubKeyCredParams: [{ type: "public-key", alg: -7 }],
      authenticatorSelection: {
        authenticatorAttachment: "platform",
        residentKey: "required",
        requireResidentKey: true,
        userVerification: "required",
      },
      extensions: { prf: { eval: { first: salt } } },
    },
  });
  if (!(credential instanceof PublicKeyCredential)) {
    throw new Error("Chromium did not create a public-key credential");
  }
  const output = (credential.getClientExtensionResults() as PrfResults).prf?.results?.first;
  if (output === undefined) throw new Error("The real passkey did not return a PRF result");
  const bytes = new Uint8Array(output);
  if (bytes.length !== 32) throw new Error("The real passkey PRF result was not 32 bytes");
  return bytes;
};

const waitForWorkerSession = async (): Promise<{ client: VaultWorkerClient; sessionId: string }> => {
  const session = browserCdp();
  await session.send("Target.setAutoAttach", {
    autoAttach: false,
    waitForDebuggerOnStart: false,
    flatten: false,
  });
  const workerSession = new Promise<string>((resolve) => {
    session.on("Target.attachedToTarget", (event: AttachedTarget) => {
      if (event.targetInfo.type === "worker") resolve(event.sessionId);
    });
  });
  await session.send("Target.setAutoAttach", {
    autoAttach: true,
    waitForDebuggerOnStart: false,
    flatten: false,
  });
  const client = new VaultWorkerClient();
  workers.push(client);
  const sessionId = await Promise.race([
    workerSession,
    new Promise<never>((_, reject) => {
      setTimeout(() => reject(new Error("The crypto Worker was not observed")), 15_000);
    }),
  ]);
  return { client, sessionId };
};

const sendToWorker = async (
  workerSessionId: string,
  method: string,
  params: object = {},
): Promise<unknown> => {
  const session = browserCdp();
  const id = nextProtocolCommandId++;
  const reply = new Promise<ProtocolReply>((resolve) => {
    session.on("Target.receivedMessageFromTarget", (event: TargetMessage) => {
      if (event.sessionId !== workerSessionId) return;
      const message = JSON.parse(event.message) as ProtocolReply;
      if (message.id === id) resolve(message);
    });
  });
  await session.send("Target.sendMessageToTarget", {
    sessionId: workerSessionId,
    message: JSON.stringify({ id, method, params }),
  });
  const message = await reply;
  if (message.error !== undefined) throw new Error(message.error.message);
  return message.result;
};

const installWorkerConsoleCapture = async (workerSessionId: string): Promise<void> => {
  await sendToWorker(workerSessionId, "Runtime.enable");
  await sendToWorker(workerSessionId, "Runtime.evaluate", {
    expression: `(() => {
      const normalise = (value) => {
        if (value instanceof ArrayBuffer) return Array.from(new Uint8Array(value));
        if (ArrayBuffer.isView(value)) {
          return Array.from(new Uint8Array(value.buffer, value.byteOffset, value.byteLength));
        }
        if (value instanceof CryptoKey) {
          return {
            __xpenseOpaqueCryptoKey: !value.extractable,
            type: value.type,
            extractable: value.extractable,
            algorithm: value.algorithm.name,
          };
        }
        return value;
      };
      globalThis.__xpenseConsoleGate = [];
      for (const method of ["log", "info", "warn", "error"]) {
        const original = console[method].bind(console);
        console[method] = (...values) => {
          globalThis.__xpenseConsoleGate.push([method, ...values.map(normalise)]);
          original(...values);
        };
      }
      console.log("xpense-worker-console-canary");
    })()`,
  });
};

const readWorkerConsole = async (workerSessionId: string): Promise<string> => {
  const result = await sendToWorker(workerSessionId, "Runtime.evaluate", {
    expression: "JSON.stringify(globalThis.__xpenseConsoleGate)",
    returnByValue: true,
  }) as { result?: { value?: string } };
  return result.result?.value ?? "";
};

const startSyncInterception = async (
  responder: (request: CapturedRequest) => JsonResponse,
): Promise<CapturedRequest[]> => {
  const captured: CapturedRequest[] = [];
  activeCapturedRequests = captured;
  activeResponder = responder;
  if (!fetchListenerInstalled) {
    browserCdp().on("Fetch.requestPaused", (event: PausedRequest) => {
      const request = {
        url: event.request.url,
        method: event.request.method,
        headers: event.request.headers,
        postData: event.request.postData ?? null,
      };
      activeCapturedRequests?.push(request);
      const response = activeResponder?.(request) ?? {};
      void browserCdp().send("Fetch.fulfillRequest", {
        requestId: event.requestId,
        responseCode: 200,
        responseHeaders: [{ name: "content-type", value: "application/json" }],
        body: responseBody(response),
      });
    });
    fetchListenerInstalled = true;
  }
  await browserCdp().send("Fetch.enable", {
    patterns: [{ urlPattern: "*://localhost*/api/v1/sync/*", requestStage: "Request" }],
  });
  fetchInterceptionEnabled = true;
  return captured;
};

const startPassiveNetworkCapture = async (): Promise<CapturedRequest[]> => {
  const captured: CapturedRequest[] = [];
  browserCdp().on("Network.requestWillBeSent", (event: NetworkRequest) => {
    captured.push({
      url: event.request.url,
      method: event.request.method,
      headers: event.request.headers,
      postData: event.request.postData ?? null,
    });
  });
  await browserCdp().send("Network.enable");
  networkCaptureEnabled = true;
  return captured;
};

const installPageConsoleCapture = (): { logs: unknown[] } => {
  const logs: unknown[] = [];
  const methods = ["log", "info", "warn", "error"] as const;
  const originals = methods.map((method) => ({ method, value: console[method] }));
  for (const { method, value } of originals) {
    console[method] = (...values: unknown[]) => {
      logs.push([method, ...values]);
      value.apply(console, values);
    };
  }
  const restore = (): void => {
    for (const { method, value } of originals) console[method] = value;
  };
  pageConsoleRestores.push(restore);
  return { logs };
};

const idbRequest = <T>(request: IDBRequest<T>): Promise<T> =>
  new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });

const inspectIndexedDb = async (): Promise<unknown[]> => {
  const output: unknown[] = [];
  for (const info of await indexedDB.databases()) {
    if (info.name === undefined) continue;
    const database = await new Promise<IDBDatabase>((resolve, reject) => {
      const request = indexedDB.open(info.name!);
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
    try {
      for (const storeName of Array.from(database.objectStoreNames)) {
        const transaction = database.transaction(storeName, "readonly");
        const store = transaction.objectStore(storeName);
        output.push({
          database: info.name,
          store: storeName,
          keys: await idbRequest(store.getAllKeys()),
          values: await idbRequest(store.getAll()),
        });
      }
    } finally {
      database.close();
    }
  }
  return output;
};

const observe = async (
  value: unknown,
  path: string,
  observations: Observation[],
  seen: Set<object>,
): Promise<void> => {
  if (value === null || value === undefined) return;
  if (typeof value === "string") {
    observations.push({ path, text: value });
    return;
  }
  if (typeof value === "number" || typeof value === "boolean" || typeof value === "bigint") {
    observations.push({ path, text: String(value) });
    return;
  }
  if (value instanceof ArrayBuffer) {
    observations.push({ path, bytes: new Uint8Array(value) });
    return;
  }
  if (ArrayBuffer.isView(value)) {
    observations.push({
      path,
      bytes: new Uint8Array(Array.from(
        new Uint8Array(value.buffer, value.byteOffset, value.byteLength),
      )),
    });
    return;
  }
  if (value instanceof Blob) {
    observations.push({ path, bytes: new Uint8Array(await value.arrayBuffer()) });
    observations.push({ path: `${path}.type`, text: value.type });
    return;
  }
  if (value instanceof CryptoKey) {
    observations.push({
      path,
      opaqueCryptoKey: !value.extractable,
      text: JSON.stringify({
        __xpenseOpaqueCryptoKey: !value.extractable,
        type: value.type,
        extractable: value.extractable,
        algorithm: value.algorithm.name,
        usages: value.usages,
      }),
    });
    if (value.extractable && value.type === "secret") {
      observations.push({ path: `${path}.raw`, bytes: new Uint8Array(await crypto.subtle.exportKey("raw", value)) });
    }
    return;
  }
  if (typeof value !== "object" || seen.has(value)) return;
  seen.add(value);
  try {
    const serialized = JSON.stringify(value);
    if (serialized !== undefined) observations.push({ path: `${path}.json`, text: serialized });
  } catch {
    observations.push({ path: `${path}.json`, text: "unserializable" });
  }
  for (const key of Reflect.ownKeys(value)) {
    await observe(
      (value as Record<PropertyKey, unknown>)[key],
      `${path}.${String(key)}`,
      observations,
      seen,
    );
  }
};

const namedStringRepresentations = (
  bytes: Uint8Array<ArrayBuffer>,
): Array<{ name: string; value: string }> => {
  const binary = Array.from(bytes, (byte) => String.fromCharCode(byte)).join("");
  const base64 = toBase64(bytes);
  const base64Url = base64.replace(/\+/gu, "-").replace(/\//gu, "_").replace(/=+$/u, "");
  const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0")).join("");
  const numeric = Array.from(bytes).join(",");
  return [
    { name: "raw binary string", value: binary },
    { name: "Base64", value: base64 },
    { name: "Base64Url", value: base64Url },
    { name: "URL-encoded Base64", value: encodeURIComponent(base64) },
    { name: "URL-encoded Base64Url", value: encodeURIComponent(base64Url) },
    { name: "lowercase hexadecimal", value: hex },
    { name: "uppercase hexadecimal", value: hex.toUpperCase() },
    {
      name: "lowercase percent bytes",
      value: Array.from(bytes, (byte) => `%${byte.toString(16).padStart(2, "0")}`).join(""),
    },
    {
      name: "uppercase percent bytes",
      value: Array.from(bytes, (byte) => `%${byte.toString(16).padStart(2, "0").toUpperCase()}`).join(""),
    },
    { name: "numeric sequence", value: numeric },
    { name: "numeric JSON array", value: `[${numeric}]` },
    {
      name: "numeric JSON object",
      value: JSON.stringify(Object.fromEntries(Array.from(bytes, (byte, index) => [index, byte]))),
    },
  ];
};

const stringRepresentations = (bytes: Uint8Array<ArrayBuffer>): string[] =>
  namedStringRepresentations(bytes).map((representation) => representation.value);

const containsBytes = (haystack: Uint8Array, needle: Uint8Array): boolean => {
  if (needle.length === 0 || needle.length > haystack.length) return false;
  for (let start = 0; start <= haystack.length - needle.length; start += 1) {
    let matched = true;
    for (let index = 0; index < needle.length; index += 1) {
      if (haystack[start + index] !== needle[index]) {
        matched = false;
        break;
      }
    }
    if (matched) return true;
  }
  return false;
};

const secretLeaks = (
  secret: Uint8Array<ArrayBuffer>,
  observations: readonly Observation[],
): Observation[] => {
  const representations = stringRepresentations(secret);
  return observations.filter((observation) =>
    observation.opaqueCryptoKey === true
      || observation.text?.includes('"__xpenseOpaqueCryptoKey":true') === true
      || (observation.bytes !== undefined
      ? containsBytes(observation.bytes, secret)
      : representations.some((representation) => observation.text?.includes(representation))),
  );
};

const assertSecretAbsent = (
  secretName: string,
  secret: Uint8Array<ArrayBuffer>,
  observations: readonly Observation[],
): void => {
  const leaks = secretLeaks(secret, observations);
  expect(leaks, `${secretName} leaked at ${leaks.map((leak) => leak.path).join(", ")}`).toEqual([]);
};

const detectorSecret = Uint8Array.from(
  { length: 32 },
  (_, index) => (index * 7 + 19) % 256,
) as Uint8Array<ArrayBuffer>;

const detectorCases: Array<{ name: string; value: unknown }> = [
  { name: "typed-array bytes", value: new Uint8Array(detectorSecret) },
  { name: "array-buffer bytes", value: new Uint8Array(detectorSecret).buffer },
  { name: "blob bytes", value: new Blob([detectorSecret]) },
  ...namedStringRepresentations(detectorSecret),
  { name: "numeric array", value: Array.from(detectorSecret) },
  {
    name: "numeric object",
    value: Object.fromEntries(Array.from(detectorSecret, (byte, index) => [index, byte])),
  },
];

afterEach(async () => {
  for (const restore of pageConsoleRestores.splice(0)) restore();
  if (networkCaptureEnabled) {
    await browserCdp().send("Network.disable");
    networkCaptureEnabled = false;
  }
  if (fetchInterceptionEnabled) {
    await browserCdp().send("Fetch.disable");
    fetchInterceptionEnabled = false;
  }
  activeCapturedRequests = null;
  activeResponder = null;
  for (const database of databases.splice(0)) database.close();
  await deleteVaultDatabase();
  for (const worker of workers.splice(0)) worker.terminate();
  await browserCdp().send("Target.setAutoAttach", {
    autoAttach: false,
    waitForDebuggerOnStart: false,
    flatten: false,
  });
  for (const authenticatorId of authenticators.splice(0)) {
    await browserCdp().send("WebAuthn.removeVirtualAuthenticator", { authenticatorId });
  }
  await browserCdp().send("WebAuthn.disable");
  localStorage.clear();
  sessionStorage.clear();
});

describe("vault browser privacy gate", () => {
  it.each(detectorCases)("fails closed for $name", async ({ value }) => {
    const observations: Observation[] = [];
    await observe(value, "detectorCanary", observations, new Set());

    expect(secretLeaks(detectorSecret, observations)).not.toEqual([]);
  });

  it("fails closed for an extractable CryptoKey carrier", async () => {
    const key = await crypto.subtle.importKey(
      "raw",
      detectorSecret,
      { name: "AES-GCM" },
      true,
      ["encrypt", "decrypt"],
    );
    const observations: Observation[] = [];
    await observe(key, "cryptoKeyCanary", observations, new Set());

    expect(secretLeaks(detectorSecret, observations)).not.toEqual([]);
  });

  it("fails closed for an opaque CryptoKey carrier", async () => {
    const key = await crypto.subtle.importKey(
      "raw",
      detectorSecret,
      { name: "AES-GCM" },
      false,
      ["encrypt", "decrypt"],
    );
    const observations: Observation[] = [];
    await observe(key, "opaqueCryptoKeyCanary", observations, new Set());

    expect(secretLeaks(detectorSecret, observations)).not.toEqual([]);
  });

  it("observes a canary written by the real crypto Worker", async () => {
    const worker = await waitForWorkerSession();
    await installWorkerConsoleCapture(worker.sessionId);

    expect(await readWorkerConsole(worker.sessionId)).toContain("xpense-worker-console-canary");
  });

  it("observes an actual axios sync request before inspecting it", async () => {
    const captured = await startSyncInterception(() => ({ observed: true }));

    await axios.post("/api/v1/sync/observation-canary", {
      marker: "xpense-network-observation-canary",
    });

    expect(captured).toHaveLength(1);
    expect(captured[0]?.postData).toContain("xpense-network-observation-canary");
  });

  it("keeps real passkey and master-key bytes out of sync, storage, and consoles", async () => {
    const allNetworkRequests = await startPassiveNetworkCapture();
    const passiveNetworkMarker = "xpense-passive-network-observation-canary";
    await fetch(`/passive-observation-canary?marker=${passiveNetworkMarker}`, {
      headers: { "x-xpense-observation-canary": passiveNetworkMarker },
    });
    expect(allNetworkRequests.some((request) =>
      request.url.includes(passiveNetworkMarker)
      && Object.entries(request.headers).some(([name, value]) =>
        name.toLowerCase() === "x-xpense-observation-canary" && value === passiveNetworkMarker
      )
    )).toBe(true);
    const prf = await realPrf();
    const pageConsole = installPageConsoleCapture();
    const pageConsoleMarker = "xpense-page-console-canary";
    const workerConsoleMarker = "xpense-worker-console-canary";
    console.log(pageConsoleMarker);
    const worker = await waitForWorkerSession();
    await installWorkerConsoleCapture(worker.sessionId);
    expect(pageConsole.logs.flat()).toContain(pageConsoleMarker);
    expect(await readWorkerConsole(worker.sessionId)).toContain(workerConsoleMarker);

    const userId = crypto.randomUUID();
    const wrapperId = crypto.randomUUID();
    const recordId = crypto.randomUUID();
    const envelopeId = crypto.randomUUID();
    const masterDescriptor = { userId, wrapperKind: "passkey" as const, wrapperId };
    const payloadDescriptor: PayloadDescriptor = {
      recordId,
      recordType: "account",
      ownerId: userId,
      revision: 1,
    };
    const envelopeDescriptor: EnvelopeDescriptor = { recordId, ownerId: userId, groupId: null };
    const wrappingKey = await derivePasskeyWrappingKey(prf);
    const masterKey = await createUserMasterKey();
    const masterKeyBytes = await exportSymmetricKey(masterKey) as Uint8Array<ArrayBuffer>;
    const wrappedMasterKey = await wrapMasterKey(wrappingKey, masterKey, masterDescriptor);
    const unwrappedMasterKey = await unwrapMasterKey(
      wrappingKey,
      wrappedMasterKey,
      masterDescriptor,
    );
    const unwrappedMasterKeyBytes = await exportSymmetricKey(
      unwrappedMasterKey,
    ) as Uint8Array<ArrayBuffer>;
    expect(unwrappedMasterKeyBytes).toEqual(masterKeyBytes);
    const unlock = await worker.client.request({
      type: "unlockWithMasterKey",
      masterKey: unwrappedMasterKey,
      userId,
    });
    expect(unlock.ok).toBe(true);
    const plaintext = new TextEncoder().encode(JSON.stringify({ name: "Privacy gate account" }));
    const encrypted = await worker.client.request<EncryptedRecordResult>({
      type: "encryptRecord",
      payload: plaintext,
      payloadDescriptor,
      personalEnvelopeDescriptor: envelopeDescriptor,
    });
    if (!encrypted.ok) throw new Error(encrypted.error.message);

    const database = await openVaultDatabase();
    databases.push(database);
    await database.put("wrappers", {
      id: wrapperId,
      kind: "passkey",
      nonce: wrappedMasterKey.nonce,
      ciphertext: wrappedMasterKey.ciphertext,
    });
    const indexedDbMarker = "xpense-indexeddb-observation-canary";
    const localStorageMarker = "xpense-local-storage-observation-canary";
    const sessionStorageMarker = "xpense-session-storage-observation-canary";
    await database.put("wrappers", { id: "observation-canary", marker: indexedDbMarker });
    localStorage.setItem("xpense-observation-canary", localStorageMarker);
    sessionStorage.setItem("xpense-observation-canary", sessionStorageMarker);
    const now = new Date().toISOString();
    const wireRecord = {
      id: recordId,
      recordType: 0,
      ownerUserId: userId,
      parentResourceId: null,
      revision: 1,
      protocolVersion: 1,
      nonce: toBase64(encrypted.value.sealedPayload.nonce),
      ciphertext: toBase64(encrypted.value.sealedPayload.ciphertext),
      envelopes: [{
        id: envelopeId,
        groupId: null,
        wrappedKey: toBase64(encrypted.value.personalEnvelope.ciphertext),
        nonce: toBase64(encrypted.value.personalEnvelope.nonce),
        encapsulatedKey: null,
        protocolVersion: 1,
      }],
      isDeleted: false,
      sequenceNumber: 1,
      createdAt: now,
      updatedAt: now,
    };
    const captured = await startSyncInterception((request) =>
      request.url.includes("observation-canary")
        ? { observed: true }
        : request.method === "POST"
          ? { records: [wireRecord] }
          : { records: [wireRecord], nextCursor: "browser-gate", hasMore: false }
    );
    const syncRequestMarker = "xpense-network-observation-canary";
    await axios.post("/api/v1/sync/observation-canary", {
      marker: syncRequestMarker,
    });
    expect(captured[0]?.postData).toContain(syncRequestMarker);
    await createSyncRecords({
      records: [{
        id: recordId,
        idempotencyKey: crypto.randomUUID(),
        recordType: 0,
        parentResourceId: null,
        protocolVersion: 1,
        nonce: encrypted.value.sealedPayload.nonce,
        ciphertext: encrypted.value.sealedPayload.ciphertext,
        personalEnvelope: {
          wrappedKey: encrypted.value.personalEnvelope.ciphertext,
          nonce: encrypted.value.personalEnvelope.nonce,
          protocolVersion: 1,
        },
      }],
    });
    await worker.client.request({ type: "lock" });
    await worker.client.request({
      type: "unlockWithMasterKey",
      masterKey: unwrappedMasterKey,
      userId,
    });
    const decryptor = {
      decrypt: async (record: VaultRecord) => {
        const personalEnvelope = record.envelopes.find((envelope) => envelope.groupId === null);
        if (personalEnvelope === undefined) throw new Error("The personal envelope is missing");
        const decrypted = await worker.client.request<Uint8Array>({
          type: "decryptRecord",
          sealedPayload: { nonce: record.nonce, ciphertext: record.ciphertext },
          payloadDescriptor: {
            recordId: record.id,
            recordType: record.recordType,
            ownerId: record.ownerId,
            revision: record.revision,
          },
          personalEnvelope: {
            nonce: personalEnvelope.nonce,
            ciphertext: personalEnvelope.wrappedKey,
          },
          personalEnvelopeDescriptor: {
            recordId: record.id,
            ownerId: record.ownerId,
            groupId: null,
          },
        });
        if (!decrypted.ok) throw new Error(decrypted.error.message);
        return decrypted.value;
      },
    };
    const syncClient = new SyncClient(database, decryptor);
    await syncClient.pull();
    expect(new TextDecoder().decode(
      await decryptor.decrypt((await database.getRecord(recordId))!),
    )).toBe(new TextDecoder().decode(plaintext));

    const observations: Observation[] = [];
    await observe(allNetworkRequests, "network", observations, new Set());
    await observe(pageConsole.logs, "pageConsole", observations, new Set());
    await observe(await readWorkerConsole(worker.sessionId), "workerConsole", observations, new Set());
    await observe(
      Object.fromEntries(Array.from({ length: localStorage.length }, (_, index) => {
        const key = localStorage.key(index)!;
        return [key, localStorage.getItem(key)];
      })),
      "localStorage",
      observations,
      new Set(),
    );
    await observe(
      Object.fromEntries(Array.from({ length: sessionStorage.length }, (_, index) => {
        const key = sessionStorage.key(index)!;
        return [key, sessionStorage.getItem(key)];
      })),
      "sessionStorage",
      observations,
      new Set(),
    );
    await observe(await inspectIndexedDb(), "indexedDb", observations, new Set());

    const observedText = observations.map((observation) => observation.text ?? "").join("\n");
    for (const marker of [
      passiveNetworkMarker,
      syncRequestMarker,
      pageConsoleMarker,
      workerConsoleMarker,
      indexedDbMarker,
      localStorageMarker,
      sessionStorageMarker,
    ]) {
      expect(observedText).toContain(marker);
    }

    assertSecretAbsent("passkey PRF", prf, observations);
    assertSecretAbsent("user master key", masterKeyBytes, observations);
    assertSecretAbsent("unwrapped user master key", unwrappedMasterKeyBytes, observations);
    prf.fill(0);
    masterKeyBytes.fill(0);
    unwrappedMasterKeyBytes.fill(0);
  });
});
