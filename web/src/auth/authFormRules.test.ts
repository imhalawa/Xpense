import { describe, expect, it } from "vitest";
import { validateAuthForm } from "./authFormRules";

const valid = { email: "person@example.test", passkeyLabel: "Laptop" };

describe("validateAuthForm", () => {
  it("accepts a plausible address", () => {
    expect(validateAuthForm(valid)).toEqual({});
  });

  it("asks for an address when it is missing", () => {
    expect(validateAuthForm({ ...valid, email: "   " }).email).toBe("An email address is required.");
  });

  it("shows the expected shape when the address is malformed", () => {
    for (const email of ["imhalawa", "imhalawa@", "@example.test", "a b@example.test"]) {
      expect(validateAuthForm({ ...valid, email }).email)
        .toBe("Enter an email address, for example name@example.com.");
    }
  });

  it("rejects an address longer than the API accepts", () => {
    const email = `${"a".repeat(250)}@example.test`;
    expect(validateAuthForm({ ...valid, email }).email)
      .toBe("The email address must not exceed 256 characters.");
  });

  it("rejects a passkey name longer than the API accepts", () => {
    expect(validateAuthForm({ ...valid, passkeyLabel: "n".repeat(101) }).passkeyLabel)
      .toBe("The passkey name must not exceed 100 characters.");
  });

  it("treats an empty passkey name as acceptable", () => {
    expect(validateAuthForm({ ...valid, passkeyLabel: "" }).passkeyLabel).toBeUndefined();
  });
});
