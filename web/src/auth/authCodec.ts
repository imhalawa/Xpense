const paddedBase64 = (value: string): string => {
  const normalized = value.replace(/-/gu, "+").replace(/_/gu, "/");
  return `${normalized}${"=".repeat((4 - normalized.length % 4) % 4)}`;
};

export const decodeBase64 = (value: string, errorMessage: string): Uint8Array<ArrayBuffer> => {
  try {
    const binary = atob(paddedBase64(value));
    if (binary.length === 0) throw new Error();
    return new Uint8Array(Array.from(binary, (character) => character.charCodeAt(0)));
  } catch {
    throw new Error(errorMessage);
  }
};

export const encodeBase64 = (value: Uint8Array): string => {
  let binary = "";
  const chunkSize = 32_768;
  for (let start = 0; start < value.length; start += chunkSize) {
    binary += String.fromCharCode(...value.subarray(start, start + chunkSize));
  }
  return btoa(binary);
};

const asRecord = (value: unknown): Record<string, unknown> | null =>
  value !== null && typeof value === "object" ? value as Record<string, unknown> : null;

export const parseCredentialOptions = (
  optionsJson: string,
  errorMessage: string,
): Record<string, unknown> => {
  let parsed: Record<string, unknown> | null;
  try {
    parsed = asRecord(JSON.parse(optionsJson));
  } catch {
    throw new Error(errorMessage);
  }
  if (parsed === null) throw new Error(errorMessage);
  return asRecord(parsed.publicKey) ?? parsed;
};

const decodeDescriptors = (value: unknown, errorMessage: string): PublicKeyCredentialDescriptor[] => {
  if (!Array.isArray(value)) return [];
  return value.flatMap((entry) => {
    const descriptor = asRecord(entry);
    if (descriptor === null || typeof descriptor.id !== "string") return [];
    return [{
      ...descriptor,
      type: "public-key" as const,
      id: decodeBase64(descriptor.id, errorMessage),
    } as PublicKeyCredentialDescriptor];
  });
};

export const registrationUserId = (
  options: Record<string, unknown>,
  errorMessage: string,
): string => {
  const user = asRecord(options.user);
  if (user === null || typeof user.id !== "string") throw new Error(errorMessage);
  const decoded = new TextDecoder().decode(decodeBase64(user.id, errorMessage));
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/u.test(decoded.toLowerCase())) {
    throw new Error(errorMessage);
  }
  return decoded;
};

export const toCreationOptions = (
  options: Record<string, unknown>,
  errorMessage: string,
): CredentialCreationOptions => {
  const user = asRecord(options.user);
  if (typeof options.challenge !== "string" || user === null || typeof user.id !== "string") {
    throw new Error(errorMessage);
  }
  return {
    publicKey: {
      ...options,
      challenge: decodeBase64(options.challenge, errorMessage),
      user: { ...user, id: decodeBase64(user.id, errorMessage) },
      excludeCredentials: decodeDescriptors(options.excludeCredentials, errorMessage),
    } as unknown as PublicKeyCredentialCreationOptions,
  };
};

export const toRequestOptions = (
  options: Record<string, unknown>,
  errorMessage: string,
): CredentialRequestOptions => {
  if (typeof options.challenge !== "string") throw new Error(errorMessage);
  return {
    publicKey: {
      ...options,
      challenge: decodeBase64(options.challenge, errorMessage),
      allowCredentials: decodeDescriptors(options.allowCredentials, errorMessage),
    } as PublicKeyCredentialRequestOptions,
  };
};

export const localPrfAssertionOptions = (
  creationOptions: Record<string, unknown>,
): CredentialRequestOptions => {
  const relyingParty = asRecord(creationOptions.rp);
  const rpId = relyingParty === null ? undefined : relyingParty.id;
  return {
    publicKey: {
      challenge: crypto.getRandomValues(new Uint8Array(32)),
      userVerification: "required",
      ...(typeof rpId === "string" ? { rpId } : {}),
    },
  };
};
