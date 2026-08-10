import axios from "axios";
import {
  browserCreatePasskey,
  browserRequestAssertion,
  type AssertionOptions,
  type RegisterDependencies,
  type RegisterRequestBody,
  type RegistrationOptions,
  type SignInDependencies,
} from "./authFlow";

const antiforgeryConfig = async (): Promise<{ headers: Record<string, string> }> => ({
  headers: {
    "X-Xpense-Antiforgery": (await axios.get<{ requestToken: string }>(
      "/api/v1/auth/antiforgery",
    )).data.requestToken,
  },
});

export const registerHttpDependencies = (
  unlockWorker: RegisterDependencies["unlockWorker"],
): RegisterDependencies => ({
  registrationOptions: async (email) => (await axios.post<RegistrationOptions>(
    "/api/v1/auth/register/options",
    { email },
    await antiforgeryConfig(),
  )).data,
  createPasskey: browserCreatePasskey,
  register: async (body: RegisterRequestBody) => (await axios.post<{ id: string }>(
    "/api/v1/auth/register",
    body,
    await antiforgeryConfig(),
  )).data,
  unlockWorker,
});

export const signInHttpDependencies = (): SignInDependencies => ({
  assertionOptions: async (email) => (await axios.post<AssertionOptions>(
    "/api/v1/auth/passkey/options",
    { email },
    await antiforgeryConfig(),
  )).data,
  requestAssertion: browserRequestAssertion,
  signIn: async (pendingPasskeyAssertionId, credentialJson) => (await axios.post<{ id: string }>(
    "/api/v1/auth/passkey/sign-in",
    { pendingPasskeyAssertionId, credentialJson },
    await antiforgeryConfig(),
  )).data,
});

/** Signs out and drops the session cookie, so the gate sends the browser back to sign-in. */
export const signOut = async (): Promise<void> => {
  await axios.post("/api/v1/auth/logout", null, await antiforgeryConfig());
};
