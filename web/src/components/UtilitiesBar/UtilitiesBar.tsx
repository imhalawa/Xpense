import Box from "@mui/material/Box";
import SideBar from "../SideBar/SideBar";
import { useLocation } from "react-router-dom";
import TransactionsUtilities from "./TransactionsUtilities/TransactionsUtilities";
import { Slide } from "@mui/material";

interface SideMenuProps {
  visible: boolean;
  onVisibilityChange: (visible: boolean) => void;
}

const UtilitiesBar = ({ visible, onVisibilityChange }: SideMenuProps) => {
  const location = useLocation();
  return (
    <Box
      width={"26rem"}
      position={"fixed"}
      display="flex"
      height="calc(100vh - 4rem)"
      flexDirection={"row"}
      justifyContent={"left"}
      sx={{ backgroundColor: "white" }}
    >
      <SideBar visible={!visible} onVisibilityChange={() => onVisibilityChange(false)} />
      <Slide in={visible} direction="down" timeout={{ enter: 500, exit: 250 }}>
        <Box>{location.pathname === "/transactions" && <TransactionsUtilities />}</Box>
      </Slide>
    </Box>
  );
};

export default UtilitiesBar;
