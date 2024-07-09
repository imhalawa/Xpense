import {Drawer, IconButton, List, ListItemButton, ListItemIcon, ListItemText} from "@mui/material";
import {Link as RouterLink} from "react-router-dom";
import * as React from "react";
import { Activity, ArrowLeftRight, Ellipsis, EllipsisVertical, Settings } from "lucide-react";

const MenuLinksDrawer = () => {
    const [open, setOpen] = React.useState(false);

    function handleOpenMenu() {
        setOpen(true);
    }

    function handleCloseMenu() {
        setOpen(false);
    }

    return (
        <>
            <IconButton color="primary" onClick={handleOpenMenu}>
                {open && <Ellipsis/>}
                {!open && <EllipsisVertical/>}
            </IconButton>

            <Drawer open={open} onClose={handleCloseMenu} anchor='bottom'>
                <List>
                    <ListItemButton onClick={handleCloseMenu} component={RouterLink} to="/">
                        <ListItemIcon>
                            <Activity/>
                        </ListItemIcon>
                        <ListItemText>
                            Dashboard
                        </ListItemText>
                    </ListItemButton>
                    <ListItemButton onClick={handleCloseMenu} component={RouterLink} to="/transactions">
                        <ListItemIcon>
                            <ArrowLeftRight/>
                        </ListItemIcon>
                        <ListItemText>
                            Transactions
                        </ListItemText>
                    </ListItemButton>
                    <ListItemButton onClick={handleCloseMenu} component={RouterLink} to="/settings">
                        <ListItemIcon>
                            <Settings/>
                        </ListItemIcon>
                        <ListItemText>
                            Settings
                        </ListItemText>
                    </ListItemButton>
                </List>
            </Drawer>
        </>
    );
}

export default MenuLinksDrawer;