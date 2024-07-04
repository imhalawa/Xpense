import Typography from "@mui/material/Typography";
import ExpensesDataGrid from "./ExpensesDataGrid/ExpensesDataGrid.tsx";
import CategoriesBar from "./ExpensesCategories/CategoriesBar.tsx";
import {Button, Grid} from "@mui/material";
import {createCategoriesFixture} from "../../fixtures.ts";
import Box from "@mui/material/Box";
import createExpenseRow from "../../typings/IExpenseRow.ts";

const Expenses = () => {

    return <>
        <Grid container spacing={3}>
            <Grid item xs={12}>
                <Box
                    display="flex"
                    alignItems="space-between"
                    justifyContent="space-between"
                >
                    <Typography variant="h3" color="textSecondary">Expenses</Typography>
                    <Box
                        display='flex'
                        alignItems='center'
                    >
                        <Button variant='contained' color='primary'>New</Button>
                    </Box>
                </Box>
            </Grid>
            <Grid item xs={12}>
                <CategoriesBar categories={categories}/>
            </Grid>
            <Grid item xs={12}>
                <ExpensesDataGrid
                    rows={rows}
                    onClick={(category) => console.log(category)}
                />
            </Grid>
        </Grid>
    </>
}

export default Expenses;