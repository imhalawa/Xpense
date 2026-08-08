import {
  Avatar,
  Body1,
  Button,
  Caption1,
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
import type { ColorSchemeMode } from "../fluent/useColorScheme";
import type { SpaceId, SpaceSummary } from "../vault/VaultProjection";

const spaceRadioName = "space";
const themeRadioName = "theme";

const themeOptions: ReadonlyArray<{ label: string; value: ColorSchemeMode }> = [
  { label: "System", value: "system" },
  { label: "Light", value: "light" },
  { label: "Dark", value: "dark" },
];

const useStyles = makeStyles({
  trigger: {
    justifyContent: "flex-start",
    columnGap: tokens.spacingHorizontalS,
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

export interface IdentityMenuProps {
  spaces: SpaceSummary[];
  activeSpace: SpaceId;
  displayName: string;
  emailPrefix: string;
  isUnlocked: boolean;
  themeMode: ColorSchemeMode;
  onSelectSpace: (space: SpaceId) => void;
  onManageGroups?: () => void;
  onAccountSettings?: () => void;
  onLock: () => void;
  onUnlock: () => void;
  onSignOut?: () => void;
  onThemeModeChange: (mode: ColorSchemeMode) => void;
}

const IdentityMenu = ({
  spaces,
  activeSpace,
  displayName,
  emailPrefix,
  isUnlocked,
  themeMode,
  onSelectSpace,
  onManageGroups,
  onAccountSettings,
  onLock,
  onUnlock,
  onSignOut,
  onThemeModeChange,
}: IdentityMenuProps) => {
  const styles = useStyles();
  const identityLabel = isUnlocked ? displayName : emailPrefix;
  const activeSpaceName = spaces.find((space) => space.id === activeSpace)?.name ?? "";
  const triggerLabel = activeSpaceName ? `${identityLabel}, ${activeSpaceName}` : identityLabel;

  return (
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
          {onManageGroups && <MenuItem onClick={onManageGroups}>Manage groups</MenuItem>}
          {onAccountSettings && (
            <MenuItem onClick={onAccountSettings}>Account and passkeys</MenuItem>
          )}
          <Menu
            checkedValues={{ [themeRadioName]: [themeMode] }}
            onCheckedValueChange={(_event, data) => {
              const [chosenMode] = data.checkedItems;
              if (chosenMode === "system" || chosenMode === "light" || chosenMode === "dark") {
                onThemeModeChange(chosenMode);
              }
            }}>
            <MenuTrigger disableButtonEnhancement>
              <MenuItem>Theme</MenuItem>
            </MenuTrigger>
            <MenuPopover>
              <MenuList>
                {themeOptions.map((option) => (
                  <MenuItemRadio key={option.value} name={themeRadioName} value={option.value}>
                    {option.label}
                  </MenuItemRadio>
                ))}
              </MenuList>
            </MenuPopover>
          </Menu>
          <MenuItem onClick={isUnlocked ? onLock : onUnlock}>
            {isUnlocked ? "Lock vault" : "Unlock vault"}
          </MenuItem>
          {onSignOut && <MenuItem onClick={onSignOut}>Sign out</MenuItem>}
        </MenuList>
      </MenuPopover>
    </Menu>
  );
};

export default IdentityMenu;
