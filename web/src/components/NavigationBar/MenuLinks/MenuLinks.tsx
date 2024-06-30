import Box from "@mui/material/Box";
import {Icon, Link} from "@mui/material";
import {AccountBalanceRounded, AccountTreeRounded, TrendingDownRounded, TrendingUpRounded} from "@mui/icons-material";
import {Link as RouterLink} from "react-router-dom";
import * as React from "react";

const MenuLinks = () => {

    return <>
        <Box mx={2} display="flex" alignItems="center">
            <Icon sx={{mr: 0.5}}>
                <TrendingDownRounded/>
            </Icon>
            <Link underline="none" color="black" component={RouterLink} to="/expenses">Expenses</Link>
        </Box>
        <Box mx={2} display="flex" alignItems="center">
            <Icon sx={{mr: 0.5}}>
                <TrendingUpRounded/>
            </Icon>
            <Link underline="none" color="black" component={RouterLink} to="/income">Income</Link>
        </Box>
        <Box mx={2} display="flex" alignItems="center">
            <Icon sx={{mr: 0.5}}>
                <AccountTreeRounded/>
            </Icon>
            <Link underline="none" color="black" component={RouterLink} to="/categories">Categories</Link>
        </Box>
        <Box mx={2} display="flex" alignItems="center">
            <Icon sx={{mr: 0.5}}>
                <AccountBalanceRounded/>
            </Icon>
            <Link underline="none" color="black" component={RouterLink} to="/accounts">Accounts</Link>
        </Box>
    </>
}

export default MenuLinks;