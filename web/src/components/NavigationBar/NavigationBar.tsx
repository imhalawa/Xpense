import AppBar from "@mui/material/AppBar";
import Toolbar from "@mui/material/Toolbar";
import Typography from "@mui/material/Typography";
import MenuLinks from "./MenuLinks/MenuLinks.tsx";
import MenuLinksDrawer from "./MenuLinksDrawer/MenuLinksDrawer.tsx";
import { LinearProgress, useMediaQuery } from "@mui/material";
import { useLoading } from "../../contexts/Loading.tsx";

const NavigationBar = () => {
  const matchMd = useMediaQuery("(min-width: 768px)");
  const { loading } = useLoading();
  return (
    <>
      <AppBar position="static" color="default">
        <Toolbar>
          <Typography variant="h6" component="div" color="black" sx={{ flexGrow: 1 }}>
            Xpense
          </Typography>
          {matchMd && <MenuLinks />}
          {!matchMd && <MenuLinksDrawer />}
        </Toolbar>
        {loading && <LinearProgress color="primary" sx={{ mt: 0 }} />}
      </AppBar>
    </>
  );
};

export default NavigationBar;
