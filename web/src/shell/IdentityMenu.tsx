import { useState } from "react";
import {
  Avatar,
  Body1,
  Button,
  Caption1,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Menu,
  MenuDivider,
  MenuItem,
  MenuItemRadio,
  MenuList,
  MenuPopover,
  MenuTrigger,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { SpaceId, SpaceSummary } from "../vault/VaultProjection";

const spaceRadioName = "space";
const manageGroupsLabel = "Manage groups";
const accountSettingsLabel = "Account and passkeys";
const lockVaultLabel = "Lock vault";
const signOutLabel = "Sign out";

const useStyles = makeStyles({
  trigger: {
    justifyContent: "flex-start",
    width: "100%",
    height: "auto",
    paddingBlock: tokens.spacingVerticalS,
    paddingInline: tokens.spacingHorizontalS,
  },
  identity: {
    display: "flex",
    flexDirection: "column",
    alignItems: "flex-start",
    minWidth: 0,
    textAlign: "start",
  },
  space: {
    color: tokens.colorNeutralForeground3,
  },
});

const describeUnavailableFeature = (feature: string) =>
  `${feature} arrives with accounts. Xpense has no sign-in, no groups and no passkeys yet, so there is nothing for this to change.`;

interface IdentityMenuProps {
  spaces: SpaceSummary[];
  activeSpace: SpaceId;
  displayName: string;
  emailPrefix: string;
  isUnlocked: boolean;
  onSelectSpace: (space: SpaceId) => void;
  onManageGroups: () => void;
  onAccountSettings: () => void;
  onLock: () => void;
  onSignOut: () => void;
}

const IdentityMenu = ({
  spaces,
  activeSpace,
  displayName,
  emailPrefix,
  isUnlocked,
  onSelectSpace,
  onManageGroups,
  onAccountSettings,
  onLock,
  onSignOut,
}: IdentityMenuProps) => {
  const styles = useStyles();
  const [unavailableFeature, setUnavailableFeature] = useState<string | null>(null);

  const identityLabel = isUnlocked ? displayName : emailPrefix;
  const activeSpaceName = spaces.find((space) => space.id === activeSpace)?.name ?? "";
  const triggerLabel = activeSpaceName ? `${identityLabel}, ${activeSpaceName}` : identityLabel;

  const announceUnavailable = (feature: string, notify: () => void) => () => {
    notify();
    setUnavailableFeature(feature);
  };

  return (
    <>
      <Menu
        checkedValues={{ [spaceRadioName]: [activeSpace] }}
        onCheckedValueChange={(_event, data) => {
          const [chosenSpace] = data.checkedItems;
          if (chosenSpace) onSelectSpace(chosenSpace);
        }}>
        <MenuTrigger disableButtonEnhancement>
          <Button className={styles.trigger} appearance="subtle" aria-label={triggerLabel}>
            <Avatar aria-hidden name={identityLabel} size={32} />
            <span className={styles.identity}>
              <Body1>{identityLabel}</Body1>
              <Caption1 className={styles.space}>{activeSpaceName}</Caption1>
            </span>
          </Button>
        </MenuTrigger>
        <MenuPopover>
          <MenuList>
            {spaces.map((space) => (
              <MenuItemRadio key={space.id} name={spaceRadioName} value={space.id}>
                {space.name}
              </MenuItemRadio>
            ))}
            <MenuDivider />
            <MenuItem onClick={announceUnavailable(manageGroupsLabel, onManageGroups)}>
              {manageGroupsLabel}
            </MenuItem>
            <MenuItem onClick={announceUnavailable(accountSettingsLabel, onAccountSettings)}>
              {accountSettingsLabel}
            </MenuItem>
            <MenuItem onClick={onLock}>{lockVaultLabel}</MenuItem>
            <MenuItem onClick={announceUnavailable(signOutLabel, onSignOut)}>
              {signOutLabel}
            </MenuItem>
          </MenuList>
        </MenuPopover>
      </Menu>

      <Dialog
        open={unavailableFeature !== null}
        onOpenChange={(_event, data) => {
          if (!data.open) setUnavailableFeature(null);
        }}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>{unavailableFeature}</DialogTitle>
            <DialogContent>
              {unavailableFeature === null ? null : describeUnavailableFeature(unavailableFeature)}
            </DialogContent>
            <DialogActions>
              <Button appearance="primary" onClick={() => setUnavailableFeature(null)}>
                Close
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </>
  );
};

export default IdentityMenu;
