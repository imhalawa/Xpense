import {Drawer, IconButton, List, ListItemButton, ListItemIcon, ListItemText} from "@mui/material";
import {AccountBalanceRounded, AccountTreeRounded, MenuOpenRounded, MenuRounded, TrendingDownRounded, TrendingUpRounded} from "@mui/icons-material";
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
                    <ListItemButton onClick={handleCloseMenu} component={RouterLink} to="/expenses">
                        <ListItemIcon>
                            <TrendingDownRounded/>
                        </ListItemIcon>
                        <ListItemText>
                            Expenses
                        </ListItemText>
                    </ListItemButton>
                    <ListItemButton onClick={handleCloseMenu} component={RouterLink} to="/income">
                        <ListItemIcon>
                            <TrendingUpRounded/>
                        </ListItemIcon>
                        <ListItemText>
                            Income
                        </ListItemText>
                    </ListItemButton>
                    <ListItemButton onClick={handleCloseMenu} component={RouterLink} to="/categories">
                        <ListItemIcon>
                            <AccountTreeRounded/>
                        </ListItemIcon>
                        <ListItemText>
                            Categories
                        </ListItemText>
                    </ListItemButton>
                    <ListItemButton onClick={handleCloseMenu} component={RouterLink} to="/accounts">
                        <ListItemIcon>
                            <AccountBalanceRounded/>
                        </ListItemIcon>
                        <ListItemText>
                            Accounts
                        </ListItemText>
                    </ListItemButton>
                </List>
            </Drawer>
        </>
    );
}

export default MenuLinksDrawer;