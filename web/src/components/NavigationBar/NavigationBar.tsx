import * as React from 'react';
import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import MenuLinks from "./MenuLinks/MenuLinks.tsx";
import MenuLinksDrawer from "./MenuLinksDrawer/MenuLinksDrawer.tsx";
import {useMediaQuery} from "@mui/material";

const NavigationBar = () => {
    const matchMd = useMediaQuery('(min-width: 768px)');


    return <>
        <AppBar position="static" color="default">
            <Toolbar>
                <Typography variant="h6" component="div" color="black" sx={{flexGrow: 1}}>
                    Xpense
                </Typography>

                {matchMd && <MenuLinks/>}

                {!matchMd && <MenuLinksDrawer/>}

            </Toolbar>
        </AppBar>
    </>
}


export default NavigationBar;