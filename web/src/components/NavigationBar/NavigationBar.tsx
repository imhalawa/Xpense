import * as React from 'react';
import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import {Icon, Link} from "@mui/material";
import {Link as RouterLink} from 'react-router-dom';
import {AccountBalanceWallet} from "@mui/icons-material";

const NavigationBar =
    () => {
        return <>
            <Box sx={{flexGrow: 1}}>
                <AppBar position="static" color="transparent">
                    <Toolbar>
                        <Icon
                            size="large"
                            edge="start"
                            color="primary"
                            aria-label="menu"
                            sx={{mr: 2}}
                        >
                            <AccountBalanceWallet/>
                        </Icon>
                        <Typography variant="h6" component="div" color="black" sx={{flexGrow: 1}}>
                            Xpense
                        </Typography>

                        <Link mx={2} underline="none" color="black" component={RouterLink} to="/expenses">Expenses</Link>
                        <Link mx={2} underline="none" color="black" component={RouterLink} to="/income">Income</Link>
                        <Link mx={2} underline="none" color="black" component={RouterLink} to="/accounts">Accounts</Link>
                        <Link mx={2} underline="none" color="black" component={RouterLink} to="/categories">Categories</Link>
                    </Toolbar>
                </AppBar>
            </Box>
        </>
    }


export default NavigationBar;