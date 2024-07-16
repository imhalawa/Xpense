import Box from "@mui/material/Box";
import { Button, Divider, Icon, Link, List, ListItem, ListItemButton, ListItemIcon, ListItemText } from "@mui/material";
import { Link as RouterLink, useLocation } from "react-router-dom";
import { Activity, ArrowLeftRight, Divide, InboxIcon, Settings } from "lucide-react";
import { DateCalendar } from "@mui/x-date-pickers";

const MenuLinks = () => {
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
    <Box display="flex" flexDirection={"column"} height="calc(100% - 64px)" justifyContent={"space-between"}>
      <List disablePadding>
        <ListItemButton component={RouterLink} to="/" sx={setActive("/")}>
          <ListItemIcon>
            <Activity size={24} />
          </ListItemIcon>
          <ListItemText>Dashboard</ListItemText>
        </ListItemButton>
        <ListItemButton component={RouterLink} to="/transactions" sx={setActive("/transactions")}>
          <ListItemIcon>
            <ArrowLeftRight size={24} />
          </ListItemIcon>
          <ListItemText>Transactions</ListItemText>
        </ListItemButton>
      </List>

      <List>
        <Divider />
        <DateCalendar sx={{ width: "100%" }} disableFuture />
        <Divider />
        <ListItemButton component={RouterLink} to="/settings" sx={setActive("/settings")}>
          <ListItemIcon>
            <Settings size={24} />
          </ListItemIcon>
          <ListItemText>Settings</ListItemText>
        </ListItemButton>
      </List>
    </Box>
  );
};

export default MenuLinks;
