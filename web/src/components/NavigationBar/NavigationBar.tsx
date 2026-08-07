import AppBar from "@mui/material/AppBar";
import Toolbar from "@mui/material/Toolbar";
import Typography from "@mui/material/Typography";
import MenuLinksDrawer from "./MenuLinksDrawer/MenuLinksDrawer.tsx";
import { LinearProgress, useMediaQuery } from "@mui/material";
import { useLoading } from "../../contexts/LoadingContext.tsx";

const NavigationBar = () => {
  const matchMd = useMediaQuery("(min-width: 900px)");
  const { loading } = useLoading();
  return (
    <>
      <AppBar position="fixed" sx={{ backgroundColor: "white" }}>
        <Toolbar color="black" sx={{ height: "100%" }}>
          <Typography variant="h6" component="div" color="black" sx={{ flexGrow: 1 }}>
            Xpense
          </Typography>
          {!matchMd && <MenuLinksDrawer />}
        </Toolbar>
        {loading && <LinearProgress color="primary" sx={{ mt: 0 }} />}
      </AppBar>
    </>
  );
};

export default NavigationBar;
