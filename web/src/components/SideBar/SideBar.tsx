import Box from "@mui/material/Box";
import { Button, Divider, List, ListItemButton, ListItemIcon, ListItemText } from "@mui/material";
import { DateCalendar } from "@mui/x-date-pickers";
import { Activity, ArrowLeftRight, ListCollapse, Settings } from "lucide-react";
import { Link, useLocation } from "react-router-dom";
import { useCalendar } from "../../contexts/CalendarContext";

interface SideMenuProps {
  onVisibilityChange: (visible: boolean) => void;
}

const SideBar = ({ onVisibilityChange }: SideMenuProps) => {
  const location = useLocation();
  const { selectedDate, setSelectedDate } = useCalendar();
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
      //   width={resizeSideBar()}
      display="flex"
      flexDirection={"column"}
      height="100%"
      sx={{ backgroundColor: "white" }}
    >
      <Box display="flex" flexDirection={"column"} height="calc(100% - 64px)" justifyContent={"space-between"}>
        <List disablePadding>
          <ListItemButton onClick={() => onVisibilityChange(false)}>
            <ListItemIcon>
              <ListCollapse size={24} />
            </ListItemIcon>
          </ListItemButton>
          <ListItemButton component={Link} to="/" sx={setActive("/")}>
            <ListItemIcon>
              <Activity size={24} />
            </ListItemIcon>
            <ListItemText>Dashboard</ListItemText>
          </ListItemButton>
          <ListItemButton component={Link} to="/transactions" sx={setActive("/transactions")}>
            <ListItemIcon>
              <ArrowLeftRight size={24} />
            </ListItemIcon>
            <ListItemText>Transactions</ListItemText>
          </ListItemButton>
        </List>
        <List>
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
          <Divider />
          <ListItemButton component={Link} to="/settings" sx={setActive("/settings")}>
            <ListItemIcon>
              <Settings size={24} />
            </ListItemIcon>
            <ListItemText>Settings</ListItemText>
          </ListItemButton>
        </List>
      </Box>
    </Box>
  );
};

export default SideBar;
