import {
  Box,
  Button,
  Divider,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import * as React from "react";
import { Activity, ArrowLeftRight, Ellipsis, EllipsisVertical, Settings } from "lucide-react";
import { DateCalendar } from "@mui/x-date-pickers";
import { useCalendar } from "../../../contexts/CalendarContext";

const MenuLinksDrawer = () => {
  const { selectedDate, setSelectedDate } = useCalendar();

  const [open, setOpen] = React.useState(false);

  function handleOpenMenu() {
    setOpen(true);
  }

  function handleCloseMenu() {
    setOpen(false);
  }

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
    <>
      <IconButton color="primary" onClick={handleOpenMenu}>
        {open && <Ellipsis />}
        {!open && <EllipsisVertical />}
      </IconButton>

      <Drawer open={open} onClose={handleCloseMenu} anchor="bottom">
        <List>
          <ListItemButton onClick={handleCloseMenu} component={RouterLink} to="/" sx={setActive("/")}>
            <ListItemIcon>
              <Activity />
            </ListItemIcon>
            <ListItemText>Dashboard</ListItemText>
          </ListItemButton>
          <ListItemButton
            onClick={handleCloseMenu}
            component={RouterLink}
            to="/transactions"
            sx={setActive("/transactions")}
          >
            <ListItemIcon>
              <ArrowLeftRight />
            </ListItemIcon>
            <ListItemText>Transactions</ListItemText>
          </ListItemButton>
          <ListItemButton onClick={handleCloseMenu} component={RouterLink} to="/settings" sx={setActive("/settings")}>
            <ListItemIcon>
              <Settings />
            </ListItemIcon>
            <ListItemText>Settings</ListItemText>
          </ListItemButton>
        </List>
        <Divider />
        <Box display="flex" flexDirection={"column"} justifyContent="center">
          <DateCalendar
            sx={{ width: "90%" }}
            value={selectedDate}
            onChange={(date) => setSelectedDate(date)}
            disableFuture
          />
          <Box display="flex" justifyContent={"center"} mb={1}>
            <Button onClick={() => setSelectedDate(null)} fullWidth>
              Clear
            </Button>
          </Box>
        </Box>
      </Drawer>
    </>
  );
};

export default MenuLinksDrawer;
