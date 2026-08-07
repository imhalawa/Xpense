import Page from "../../components/Page/Page";
import ThemeModeToggle from "../../components/ThemeModeToggle/ThemeModeToggle";

const Settings = () => {
  return (
    <Page title="Settings" headerColor="primary.dark" headerBackgroundColor="background.paper">
      <ThemeModeToggle />
    </Page>
  );
};

export default Settings;
