export interface AuthFormValues {
  email: string;
  passkeyLabel: string;
}

export type AuthFormErrors = Partial<Record<keyof AuthFormValues, string>>;

const emailMaximumLength = 256;
const passkeyLabelMaximumLength = 100;

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/u;

export const validateAuthForm = (values: AuthFormValues): AuthFormErrors => {
  const errors: AuthFormErrors = {};
  const email = values.email.trim();

  if (email === "") errors.email = "An email address is required.";
  else if (!emailPattern.test(email)) {
    errors.email = "Enter an email address, for example name@example.com.";
  } else if (email.length > emailMaximumLength) {
    errors.email = "The email address must not exceed 256 characters.";
  }

  if (values.passkeyLabel.trim().length > passkeyLabelMaximumLength) {
    errors.passkeyLabel = "The passkey name must not exceed 100 characters.";
  }

  return errors;
};
