import { useEffect, useState } from "react";
import {
  Badge,
  Body1,
  Button,
  Caption1,
  Title3,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { AddRegular, DeleteRegular, EditRegular } from "@fluentui/react-icons";
import { useAccountActions, useResourceVersion } from "../../contexts/ResourceVersionContext";
import { useVault } from "../../vault/VaultProvider";
import type { AccountView } from "../../vault/VaultProjection";

const fallbackSpace = "personal";

const newAccount: AccountView = {
  id: "",
  label: "",
  currency: "EUR" as AccountView["currency"],
  canEdit: true,
};

const useStyles = makeStyles({
  sections: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalXXXL },
  section: { display: "flex", flexDirection: "column", gap: tokens.spacingVerticalM },
  heading: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    gap: tokens.spacingHorizontalM,
  },
  card: {
    display: "flex",
    flexDirection: "column",
    overflow: "hidden",
    backgroundColor: tokens.colorNeutralBackground1,
    borderRadius: tokens.borderRadiusLarge,
    borderTopWidth: tokens.strokeWidthThin,
    borderRightWidth: tokens.strokeWidthThin,
    borderBottomWidth: tokens.strokeWidthThin,
    borderLeftWidth: tokens.strokeWidthThin,
    borderTopStyle: "solid",
    borderRightStyle: "solid",
    borderBottomStyle: "solid",
    borderLeftStyle: "solid",
    borderTopColor: tokens.colorNeutralStroke2,
    borderRightColor: tokens.colorNeutralStroke2,
    borderBottomColor: tokens.colorNeutralStroke2,
    borderLeftColor: tokens.colorNeutralStroke2,
    boxShadow: tokens.shadow4,
  },
  row: {
    display: "flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalM,
    minHeight: "56px",
    paddingInline: tokens.spacingHorizontalL,
    transitionProperty: "background-color",
    transitionDuration: tokens.durationFast,
    transitionTimingFunction: tokens.curveEasyEase,
    ":hover": { backgroundColor: tokens.colorSubtleBackgroundHover },
  },
  separated: {
    borderBottomWidth: tokens.strokeWidthThin,
    borderBottomStyle: "solid",
    borderBottomColor: tokens.colorNeutralStroke2,
  },
  label: { flexGrow: 1, minWidth: 0 },
  balance: { fontVariantNumeric: "tabular-nums", color: tokens.colorNeutralForeground2 },
  actions: { display: "flex", gap: tokens.spacingHorizontalXS, flexShrink: 0 },
  supporting: { color: tokens.colorNeutralForeground2 },
  empty: { paddingBlock: tokens.spacingVerticalXXL, paddingInline: tokens.spacingHorizontalL },
});

const majorUnits = (minorUnits: number | undefined): string =>
  ((minorUnits ?? 0) / 100).toFixed(2);

const Settings = () => {
  const styles = useStyles();
  const { projection, state } = useVault();
  const resourceVersion = useResourceVersion();
  const openAccount = useAccountActions();
  const [accounts, setAccounts] = useState<AccountView[]>([]);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    if (state !== "unlocked") {
      setAccounts([]);
      setLoaded(false);
      return;
    }

    let isCurrent = true;
    projection
      .listAccounts(fallbackSpace)
      .then((loadedAccounts) => {
        if (!isCurrent) return;
        setAccounts(loadedAccounts);
        setLoaded(true);
      })
      .catch(() => {
        if (isCurrent) setLoaded(true);
      });

    return () => {
      isCurrent = false;
    };
  }, [projection, resourceVersion, state]);

  return (
    <div className={styles.sections}>
      <section className={styles.section} aria-labelledby="settings-accounts-heading">
        <div className={styles.heading}>
          <Title3 as="h2" id="settings-accounts-heading">
            Accounts
          </Title3>
          {openAccount !== undefined && (
            <Button
              appearance="primary"
              icon={<AddRegular />}
              onClick={(event) => openAccount("create", newAccount, event.currentTarget)}>
              Add account
            </Button>
          )}
        </div>
        <Body1 className={styles.supporting}>
          Each account holds one currency for its whole life. Exactly one is the default,
          used first when you record a transaction.
        </Body1>
        <div className={styles.card}>
          {accounts.length === 0 ? (
            <Caption1 className={`${styles.empty} ${styles.supporting}`}>
              {loaded ? "No accounts yet. Add one to start your ledger." : "Loading accounts…"}
            </Caption1>
          ) : (
            accounts.map((account, index) => (
              <div
                key={account.id}
                className={
                  index < accounts.length - 1 ? `${styles.row} ${styles.separated}` : styles.row
                }>
                <Body1 className={styles.label}>{account.label}</Body1>
                {account.isDefault === true && <Badge appearance="tint">Default</Badge>}
                <Caption1 className={styles.balance}>
                  {majorUnits(account.openingBalanceMinorUnits ?? account.balanceMinorUnits)}{" "}
                  {account.currency}
                </Caption1>
                <div className={styles.actions}>
                  {openAccount !== undefined && (
                    <Button
                      appearance="subtle"
                      icon={<EditRegular />}
                      aria-label={`Edit ${account.label}`}
                      disabled={!account.canEdit}
                      onClick={(event) => openAccount("edit", account, event.currentTarget)}
                    />
                  )}
                  {openAccount !== undefined && (
                    <Button
                      appearance="subtle"
                      icon={<DeleteRegular />}
                      aria-label={`Delete ${account.label}`}
                      disabled={!account.canEdit}
                      onClick={(event) => openAccount("delete", account, event.currentTarget)}
                    />
                  )}
                </div>
              </div>
            ))
          )}
        </div>
      </section>
    </div>
  );
};

export default Settings;
