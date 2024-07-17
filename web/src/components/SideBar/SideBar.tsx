import Box from "@mui/material/Box";
import { Divider, List, ListItemButton, ListItemIcon } from "@mui/material";
import { Activity, ArrowLeftRight, ListCollapse, Settings } from "lucide-react";
import { Link, useLocation } from "react-router-dom";

interface MiniSideBarProps {
  visible: boolean;
  onVisibilityChange: (visible: boolean) => void;
}

const SideBar = ({ visible, onVisibilityChange }: MiniSideBarProps) => {
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
      display="flex"
      flexDirection={"column"}
      width={"4rem"}
      position={visible ? "fixed" : "initial"}
      height="calc(100vh - 4rem)"
      justifyContent={"space-between"}
      sx={{
        borderRight: "1px solid gray",
        backgroundColor: "primary.dark",
        color: "white",
      }}
    >
      <List>
        <ListItemButton onClick={() => onVisibilityChange(true)}>
          <ListItemIcon>
            <ListCollapse size={20} color="white" />
          </ListItemIcon>
        </ListItemButton>
        <Divider color="white" />
        <ListItemButton component={Link} to="/" sx={setActive("/")}>
          <ListItemIcon>
            <Activity size={20} color="white" />
          </ListItemIcon>
        </ListItemButton>
        <Divider color="white" />
        <ListItemButton component={Link} to="/transactions" sx={setActive("/transactions")}>
          <ListItemIcon>
            <ArrowLeftRight size={20} color="white" />
          </ListItemIcon>
        </ListItemButton>
        <Divider />
      </List>
      <List>
        <Divider color="white" />
        <ListItemButton component={Link} to="/settings" sx={setActive("/settings")}>
          <ListItemIcon>
            <Settings size={24} color="white" />
          </ListItemIcon>
        </ListItemButton>
      </List>
    </Box>
  );
};

export default SideBar;
