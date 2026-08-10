import {
  Body1,
  Button,
  Card,
  Field,
  Input,
  MessageBar,
  MessageBarBody,
  Title1,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { useState, type FormEvent } from "react";
import { Link as RouterLink, useNavigate, useSearchParams } from "react-router";
import { registerHttpDependencies, signInHttpDependencies } from "../../auth/authApi";
import Wordmark from "../../fluent/Wordmark";
import { registerWithPasskey, signInWithPasskey } from "../../auth/authFlow";
import { validateAuthForm } from "../../auth/authFormRules";
import { useVault } from "../../vault/VaultProvider";

const useStyles = makeStyles({
  page: {
    minHeight: "100vh",
    boxSizing: "border-box",
    display: "grid",
    placeItems: "center",
    padding: tokens.spacingHorizontalXXL,
    backgroundColor: tokens.colorNeutralBackground2,
  },
  card: {
    width: "min(100%, 440px)",
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingHorizontalXXL,
  },
  copy: { color: tokens.colorNeutralForeground2 },
  form: { display: "grid", gap: tokens.spacingVerticalM },
  footer: { color: tokens.colorNeutralForeground2 },
});

const messageFor = (error: unknown, fallback: string): string => {
  if (error instanceof DOMException) return "The passkey prompt was dismissed or refused.";
  return error instanceof Error && error.message !== "" ? error.message : fallback;
};

export const Auth = ({ mode }: { mode: "signIn" | "register" }) => {
  const styles = useStyles();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { unlockWithMasterKey } = useVault();
  const [email, setEmail] = useState("");
  const [passkeyLabel, setPasskeyLabel] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [touched, setTouched] = useState(false);
  const [busy, setBusy] = useState(false);

  const registering = mode === "register";
  const errors = validateAuthForm({ email, passkeyLabel });
  const shown = touched ? errors : {};

  const submit = async (event: FormEvent): Promise<void> => {
    event.preventDefault();
    if (busy) return;
    setTouched(true);
    if (Object.keys(errors).length > 0) return;
    setBusy(true);
    setError(null);
    try {
      if (registering) {
        await registerWithPasskey(registerHttpDependencies(unlockWithMasterKey), {
          email: email.trim(),
          passkeyLabel: passkeyLabel.trim() === "" ? null : passkeyLabel.trim(),
          invitationToken: searchParams.get("invitation"),
        });
      } else {
        await signInWithPasskey(signInHttpDependencies(), email.trim());
      }
      await navigate("/", { replace: true });
    } catch (failure) {
      setError(messageFor(
        failure,
        registering ? "The account could not be created." : "The passkey did not sign you in.",
      ));
    } finally {
      setBusy(false);
    }
  };

  return (
    <main className={styles.page}>
      <Card className={styles.card}>
        <Wordmark size={36} />
        <Title1 as="h1">{registering ? "Create your account" : "Welcome back"}</Title1>
        <Body1 className={styles.copy}>
          {registering
            ? "Xpense protects your records with a passkey. The keys stay on your devices — the server never sees them."
            : "Use the passkey you registered on this device."}
        </Body1>
        {error !== null && (
          <MessageBar intent="error">
            <MessageBarBody>{error}</MessageBarBody>
          </MessageBar>
        )}
        <form className={styles.form} noValidate onSubmit={(event) => void submit(event)}>
          <Field
            label="Email"
            required
            validationState={shown.email === undefined ? "none" : "error"}
            validationMessage={shown.email}>
            <Input
              type="email"
              name="email"
              autoComplete="username webauthn"
              value={email}
              onBlur={() => setTouched(true)}
              onChange={(_, data) => setEmail(data.value)}
            />
          </Field>
          {registering && (
            <Field
              label="Passkey name"
              hint="Helps you tell your devices apart later."
              validationState={shown.passkeyLabel === undefined ? "none" : "error"}
              validationMessage={shown.passkeyLabel}>
              <Input
                name="passkeyLabel"
                value={passkeyLabel}
                placeholder="This device"
                onChange={(_, data) => setPasskeyLabel(data.value)}
              />
            </Field>
          )}
          <Button appearance="primary" type="submit" disabled={busy}>
            {busy
              ? registering ? "Creating…" : "Signing in…"
              : registering ? "Create account" : "Sign in"}
          </Button>
        </form>
        <Body1 className={styles.footer}>
          {registering
            ? <>Already have an account? <RouterLink to="/signin">Sign in</RouterLink></>
            : <>No account yet? <RouterLink to="/register">Create one</RouterLink></>}
        </Body1>
      </Card>
    </main>
  );
};

export default Auth;
