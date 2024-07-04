import {Drawer, IconButton, List, ListItemButton, ListItemIcon, ListItemText} from "@mui/material";
import {
    DataUsageOutlined,
    MenuOpenRounded,
    MenuRounded, ReceiptLongOutlined, SettingsOutlined,
} from "@mui/icons-material";
import {Link as RouterLink} from "react-router-dom";
import * as React from "react";

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
                {open && <MenuOpenRounded/>}
                {!open && <MenuRounded/>}
            </IconButton>

            <Drawer open={open} onClose={handleCloseMenu} anchor='bottom'>
                <List>
                    <ListItemButton onClick={handleCloseMenu} component={RouterLink} to="/">
                        <ListItemIcon>
                            <DataUsageOutlined/>
                        </ListItemIcon>
                        <ListItemText>
                            Dashboard
                        </ListItemText>
                    </ListItemButton>
                    <ListItemButton onClick={handleCloseMenu} component={RouterLink} to="/transactions">
                        <ListItemIcon>
                            <ReceiptLongOutlined/>
                        </ListItemIcon>
                        <ListItemText>
                            Transactions
                        </ListItemText>
                    </ListItemButton>
                    <ListItemButton onClick={handleCloseMenu} component={RouterLink} to="/settings">
                        <ListItemIcon>
                            <SettingsOutlined/>
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