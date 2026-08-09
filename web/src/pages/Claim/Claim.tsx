import {
  Body1,
  Button,
  Card,
  Label,
  MessageBar,
  MessageBarBody,
  ProgressBar,
  Select,
  Title1,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ClaimHttpApi } from "../../claim/claimApi";
import {
  loadLegacyClaimPasskeys,
  unlockLegacyClaim,
  type ClaimPasskeySelection,
} from "../../claim/claimUnlock";
import {
  runLegacyClaim,
  type ClaimApi,
  type ClaimCompletion,
  type ClaimRecordCipher,
  type ClaimStatus,
  type RunLegacyClaimOptions,
} from "../../claim/claimFlow";
import { openVaultDatabase, type VaultDatabase } from "../../vault/vaultDatabase";
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
    width: "min(100%, 560px)",
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingHorizontalXXL,
  },
  copy: {
    color: tokens.colorNeutralForeground2,
  },
  progress: {
    display: "grid",
    gap: tokens.spacingVerticalS,
  },
  actions: {
    display: "flex",
    gap: tokens.spacingHorizontalM,
  },
  passkey: {
    display: "grid",
    gap: tokens.spacingVerticalXS,
  },
});

type ViewState =
  | { kind: "idle" }
  | { kind: "running"; status: ClaimStatus }
  | { kind: "failed"; message: string }
  | { kind: "complete"; completion: ClaimCompletion };

export interface ClaimViewDependencies {
  api: ClaimApi;
  cipher: ClaimRecordCipher;
  openDatabase(): Promise<VaultDatabase>;
  unlock(wrapperId: string): Promise<boolean>;
  run(options: RunLegacyClaimOptions): Promise<ClaimCompletion>;
}

export interface ClaimViewProps {
  encryptionReady: boolean;
  dependencies: ClaimViewDependencies;
  passkeys?: ClaimPasskeySelection[];
  passkeyLoadError?: string | null;
}

const phaseCopy: Record<ClaimStatus["phase"], string> = {
  starting: "Starting the protected claim…",
  downloading: "Downloading the legacy dataset…",
  validating: "Validating every legacy record…",
  encrypting: "Encrypting records in your vault…",
  uploading: "Saving encrypted progress…",
  verifying: "Verifying counts and manifest…",
  complete: "Legacy data encrypted and verified.",
};

const isAbort = (error: unknown): boolean =>
  typeof error === "object" && error !== null && "name" in error && error.name === "AbortError";

export const ClaimView = ({
  encryptionReady,
  dependencies,
  passkeys = [],
  passkeyLoadError = null,
}: ClaimViewProps) => {
  const styles = useStyles();
  const activeController = useRef<AbortController | null>(null);
  const [viewState, setViewState] = useState<ViewState>({ kind: "idle" });
  const [unlockError, setUnlockError] = useState<string | null>(null);
  const [unlocking, setUnlocking] = useState(false);
  const [selectedPasskeyId, setSelectedPasskeyId] = useState(
    passkeys.length === 1 ? passkeys[0]!.id : "",
  );

  useEffect(() => {
    setSelectedPasskeyId((current) => {
      if (passkeys.some((passkey) => passkey.id === current)) return current;
      return passkeys.length === 1 ? passkeys[0]!.id : "";
    });
  }, [passkeys]);

  useEffect(() => {
    if (encryptionReady) return;
    activeController.current?.abort();
    activeController.current = null;
    setViewState({ kind: "idle" });
  }, [encryptionReady]);

  useEffect(() => () => {
    activeController.current?.abort();
    activeController.current = null;
  }, []);

  const start = useCallback(async () => {
    if (!encryptionReady || activeController.current !== null) return;
    const controller = new AbortController();
    activeController.current = controller;
    setViewState({
      kind: "running",
      status: { phase: "starting", completedRecords: 0, totalRecords: 0 },
    });
    let database: VaultDatabase | null = null;
    try {
      database = await dependencies.openDatabase();
      const completion = await dependencies.run({
        api: dependencies.api,
        cipher: dependencies.cipher,
        database,
        signal: controller.signal,
        onStatus: (status) => setViewState({ kind: "running", status }),
      });
      if (!controller.signal.aborted) setViewState({ kind: "complete", completion });
    } catch (error) {
      if (isAbort(error)) {
        setViewState({ kind: "idle" });
      } else {
        setViewState({
          kind: "failed",
          message: error instanceof Error ? error.message : "The legacy claim could not continue.",
        });
      }
    } finally {
      database?.close();
      if (activeController.current === controller) activeController.current = null;
    }
  }, [dependencies, encryptionReady]);

  const cancel = useCallback(() => {
    activeController.current?.abort();
  }, []);

  const unlock = useCallback(async () => {
    if (unlocking || selectedPasskeyId === "") return;
    setUnlocking(true);
    setUnlockError(null);
    try {
      if (!await dependencies.unlock(selectedPasskeyId)) {
        setUnlockError("The passkey did not unlock the vault.");
      }
    } catch (error) {
      setUnlockError(error instanceof Error ? error.message : "The passkey could not unlock the vault.");
    } finally {
      setUnlocking(false);
    }
  }, [dependencies, selectedPasskeyId, unlocking]);

  const runningStatus = viewState.kind === "running" ? viewState.status : null;
  const progress = runningStatus === null || runningStatus.totalRecords === 0
    ? undefined
    : runningStatus.completedRecords / runningStatus.totalRecords;

  return (
    <main className={styles.page}>
      <Card className={styles.card}>
        <Title1 as="h1">Claim legacy data</Title1>
        <Body1 className={styles.copy}>
          This maintenance flow encrypts the existing financial data with your unlocked vault.
          Plaintext source data remains unchanged until the operator completes the later removal gate.
        </Body1>

        {!encryptionReady && (
          <>
            <MessageBar intent="warning">
              <MessageBarBody>Unlock your vault before starting the claim.</MessageBarBody>
            </MessageBar>
            {unlockError !== null && (
              <MessageBar intent="error"><MessageBarBody>{unlockError}</MessageBarBody></MessageBar>
            )}
            {passkeyLoadError !== null && (
              <MessageBar intent="error"><MessageBarBody>{passkeyLoadError}</MessageBarBody></MessageBar>
            )}
            {passkeys.length > 0 && (
              <div className={styles.passkey}>
                <Label htmlFor="claim-passkey">Passkey</Label>
                <Select
                  id="claim-passkey"
                  value={selectedPasskeyId}
                  onChange={(event) => setSelectedPasskeyId(event.target.value)}
                >
                  {passkeys.length > 1 && <option value="">Choose a passkey</option>}
                  {passkeys.map((passkey) => (
                    <option key={passkey.id} value={passkey.id}>{passkey.label}</option>
                  ))}
                </Select>
              </div>
            )}
            <Button
              appearance="primary"
              disabled={unlocking || selectedPasskeyId === ""}
              onClick={() => void unlock()}
            >
              {unlocking ? "Unlocking…" : "Unlock with passkey"}
            </Button>
          </>
        )}

        {runningStatus !== null && (
          <div className={styles.progress} aria-live="polite">
            <Body1>{phaseCopy[runningStatus.phase]}</Body1>
            <ProgressBar value={progress} />
            {runningStatus.totalRecords > 0 && (
              <Body1>
                {runningStatus.completedRecords} of {runningStatus.totalRecords} encrypted
              </Body1>
            )}
          </div>
        )}

        {viewState.kind === "failed" && (
          <MessageBar intent="error">
            <MessageBarBody>
              <div>{viewState.message}</div>
              <div>Encrypted progress is safe. Start again to resume.</div>
            </MessageBarBody>
          </MessageBar>
        )}

        {viewState.kind === "complete" && (
          <MessageBar intent="success">
            <MessageBarBody>
              <div>Legacy data encrypted and verified.</div>
              <div>{viewState.completion.recordCount} records verified</div>
            </MessageBarBody>
          </MessageBar>
        )}

        <div className={styles.actions}>
          {viewState.kind === "running" ? (
            <Button appearance="secondary" onClick={cancel}>Cancel claim</Button>
          ) : viewState.kind !== "complete" ? (
            <Button appearance="primary" disabled={!encryptionReady} onClick={() => void start()}>
              {viewState.kind === "failed" ? "Resume claim" : "Start claim"}
            </Button>
          ) : null}
        </div>
      </Card>
    </main>
  );
};

const Claim = () => {
  const { claimEncryptionReady, encryptClaimRecord, projection, unlockWithMasterKey } = useVault();
  const [passkeys, setPasskeys] = useState<ClaimPasskeySelection[]>([]);
  const [passkeyLoadError, setPasskeyLoadError] = useState<string | null>(null);

  useEffect(() => {
    if (projection.dataMode === "unknown") void projection.unlock().catch(() => undefined);
  }, [projection]);

  useEffect(() => {
    if (claimEncryptionReady) return;
    let active = true;
    setPasskeyLoadError(null);
    void loadLegacyClaimPasskeys().then(
      (loaded) => {
        if (active) setPasskeys(loaded);
      },
      () => {
        if (active) setPasskeyLoadError("The passkeys for this account could not be loaded.");
      },
    );
    return () => {
      active = false;
    };
  }, [claimEncryptionReady]);

  const dependencies = useMemo<ClaimViewDependencies>(() => ({
    api: new ClaimHttpApi(),
    cipher: {
      isReady: () => claimEncryptionReady,
      encrypt: encryptClaimRecord,
    },
    openDatabase: openVaultDatabase,
    unlock: (wrapperId) => unlockLegacyClaim(unlockWithMasterKey, wrapperId),
    run: runLegacyClaim,
  }), [claimEncryptionReady, encryptClaimRecord, unlockWithMasterKey]);

  return (
    <ClaimView
      encryptionReady={claimEncryptionReady}
      dependencies={dependencies}
      passkeys={passkeys}
      passkeyLoadError={passkeyLoadError}
    />
  );
};

export default Claim;
