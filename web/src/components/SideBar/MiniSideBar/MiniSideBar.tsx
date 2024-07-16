import Box from "@mui/material/Box";
import { Button, Divider, List, ListItem, ListItemButton, ListItemIcon, useMediaQuery } from "@mui/material";
import { Activity, ArrowLeftRight, ListCollapse, Settings } from "lucide-react";
import { Link, useLocation } from "react-router-dom";

interface MiniSideBarProps {
  onVisibilityChange: (visible: boolean) => void;
}

const MiniSideBar = ({ onVisibilityChange }: MiniSideBarProps) => {
  const location = useLocation();

  const setActive = (path: string) => {
    if (location.pathname === path) {
      return {
        backgroundColor: "primary.light",
        color: "white",
        ":hover": {
          backgroundColor: "primary.main",
          color: "black",
        },
      };
    }
  };

  return (
    <Box
      position={"fixed"}
      display="flex"
      flexDirection={"column"}
      height="100%"
      sx={{ backgroundColor: "white" }}
    >
      <Box display="flex" flexDirection={"column"} height="calc(100% - 64px)" justifyContent={"space-between"}>
        <List disablePadding>
          <ListItem sx={{ margin: 0, padding: 0 }}>
            <Button onClick={() => onVisibilityChange(true)}>
              <ListCollapse />
            </Button>
          </ListItem>
          <ListItemButton component={Link} to="/" sx={setActive("/")}>
            <ListItemIcon>
              <Activity size={24} />
            </ListItemIcon>
          </ListItemButton>
          <ListItemButton component={Link} to="/transactions" sx={setActive("/transactions")}>
            <ListItemIcon>
              <ArrowLeftRight size={24} />
            </ListItemIcon>
          </ListItemButton>
        </List>
        <List>
          <Divider />
          <ListItemButton component={Link} to="/settings" sx={setActive("/settings")}>
            <ListItemIcon>
              <Settings size={24} />
            </ListItemIcon>
          </ListItemButton>
        </List>
      </Box>
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

export default MiniSideBar;
