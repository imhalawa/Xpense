export interface ClaimRecordIdentity {
  recordType: string;
  id: string;
}

const encoder = new TextEncoder();

export const buildClaimManifest = (
  identities: readonly ClaimRecordIdentity[],
): string => {
  const lines = identities
    .map((identity) => `${identity.recordType}|${identity.id}`.toLowerCase())
    .sort((left, right) => left < right ? -1 : left > right ? 1 : 0);
  return lines.length === 0 ? "0" : `${lines.length}\n${lines.join("\n")}`;
};

export const hashClaimManifest = async (
  identities: readonly ClaimRecordIdentity[],
): Promise<string> => {
  const digest = new Uint8Array(
    await crypto.subtle.digest("SHA-256", encoder.encode(buildClaimManifest(identities))),
  );
  return Array.from(digest, (byte) => byte.toString(16).padStart(2, "0")).join("");
};
