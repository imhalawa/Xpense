import Box from "@mui/material/Box";
import { Icon, Link } from "@mui/material";
import { Link as RouterLink } from "react-router-dom";
import { Activity, ArrowLeftRight, Settings } from "lucide-react";

const MenuLinks = () => {
    return <>
        <Link underline="none" color="neutral" component={RouterLink} to="/">
            <Box mx={2} display="flex" alignItems="center">
                <Icon component={Activity} sx={{ mr: 0.5 }} />
                Dashboard
            </Box>
        </Link>
        <Link underline="hover" color="black" component={RouterLink} to="/transactions">
            <Box mx={2} display="flex" alignItems="center">
                <Icon component={ArrowLeftRight} sx={{ mr: 0.5 }} />
                Transactions
            </Box>
        </Link>
        <Link underline="hover" color="black" component={RouterLink} to="/settings">
            <Box mx={2} display="flex" alignItems="center">
                <Icon component={Settings} sx={{ mr: 0.5 }} />
                Settings
            </Box>
        </Link>
    </>
}

export default MenuLinks;