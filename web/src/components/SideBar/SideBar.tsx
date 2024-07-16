import Box from "@mui/material/Box";
import MenuLinks from "../NavigationBar/MenuLinks/MenuLinks";
import { useMediaQuery } from "@mui/material";

const SideBar = () => {
  return (
    <Box
      position={"fixed"}
      width={resizeSideBar()}
      display="flex"
      flexDirection={"column"}
      height="100%"
      sx={{ backgroundColor: "white" }}
    >
      <MenuLinks />
    </Box>
  );
};

const resizeSideBar = () => {
  const matchSM = useMediaQuery("(min-width: 600px)");
  const matchMd = useMediaQuery("(min-width: 900px)");
  const matchLg = useMediaQuery("(min-width: 1200px)");
  const matchXL = useMediaQuery("(min-width: 2000px)");

  if (matchXL) {
    return "8.3%";
  } else if (matchLg) {
    return "16.6%";
  } else if (matchMd) {
    return "25%";
  } else if (matchSM) {
    return "33.3%";
  }
};

export default SideBar;
