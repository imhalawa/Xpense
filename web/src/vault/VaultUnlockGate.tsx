import { Body1, Button, Card, Label, MessageBar, MessageBarBody, Select, Spinner, Title2, makeStyles, tokens } from "@fluentui/react-components";
import { useEffect, useState, type ReactNode } from "react";
import { Navigate } from "react-router";
import { loadLegacyClaimPasskeys, unlockLegacyClaim, type ClaimPasskeySelection } from "../claim/claimUnlock";
import { useVault } from "./VaultProvider";

const useStyles = makeStyles({
  page: { minHeight: "100vh", display: "grid", placeItems: "center", backgroundColor: tokens.colorNeutralBackground2 },
  card: { width: "min(90vw, 440px)", gap: tokens.spacingVerticalM, padding: tokens.spacingHorizontalXL },
});

export const VaultUnlockGate = ({ children }: { children: ReactNode }) => {
  const styles = useStyles();
  const { projection, state, unlockWithMasterKey } = useVault();
  const [passkeys, setPasskeys] = useState<ClaimPasskeySelection[]>([]);
  const [selected, setSelected] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [modeError, setModeError] = useState(false);
  const [unlocking, setUnlocking] = useState(false);

  useEffect(() => {
    if (projection.dataMode !== "unknown") return;
    void projection.unlock().catch(() => setModeError(true));
  }, [projection]);

  useEffect(() => {
    if (projection.dataMode !== "encrypted" || state === "unlocked") return;
    let current = true;
    loadLegacyClaimPasskeys().then((values) => {
      if (!current) return;
      setPasskeys(values);
      setSelected(values.length === 1 ? values[0]!.id : "");
    }).catch(() => current && setError("Passkey wrappers could not be loaded."));
    return () => { current = false; };
  }, [projection.dataMode, state]);

  if (projection.dataMode === "claiming") return <Navigate to="/claim" replace />;
  if (state === "unlocked") return children;
  if (projection.dataMode === "unknown") return <div className={styles.page}>{modeError ? <MessageBar intent="error"><MessageBarBody>The protected data mode could not be verified. Xpense will not open legacy data.</MessageBarBody></MessageBar> : <Spinner label="Checking protected data mode" />}</div>;

  const unlock = async (): Promise<void> => {
    if (selected === "") return;
    setUnlocking(true);
    setError(null);
    try {
      const unlocked = await unlockLegacyClaim(unlockWithMasterKey, selected);
      if (!unlocked) setError("The passkey did not unlock the vault.");
    } catch (unlockError) {
      setError(unlockError instanceof Error ? unlockError.message : "The passkey could not unlock the vault.");
    } finally {
      setUnlocking(false);
    }
  };

  return (
    <main className={styles.page}>
      <Card className={styles.card}>
        <Title2 as="h1">Unlock Xpense</Title2>
        <Body1>Your encrypted records stay on this device only while the vault is unlocked.</Body1>
        {error !== null && <MessageBar intent="error"><MessageBarBody>{error}</MessageBarBody></MessageBar>}
        <Label htmlFor="vault-passkey">Passkey</Label>
        <Select id="vault-passkey" value={selected} onChange={(event) => setSelected(event.target.value)}>
          {passkeys.length !== 1 && <option value="">Choose a passkey</option>}
          {passkeys.map((passkey) => <option key={passkey.id} value={passkey.id}>{passkey.label}</option>)}
        </Select>
        <Button appearance="primary" disabled={selected === "" || unlocking} onClick={() => void unlock()}>{unlocking ? "Unlocking…" : "Unlock"}</Button>
      </Card>
    </main>
  );
};
