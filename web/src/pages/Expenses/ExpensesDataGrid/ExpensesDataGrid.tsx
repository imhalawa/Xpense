import * as React from 'react';
import Table, {TablePropsSizeOverrides} from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Paper from '@mui/material/Paper';
import {IExpenseRow} from "../../../typings/IExpenseRow.ts";
import CategoryChip from "../ExpenseCategoryChip/CategoryChip.tsx";
import {ICategory} from "../../../typings/ICategory.ts";

interface IExpenseDataGridProps {
    rows: IExpenseRow[];
    onClick: (category: ICategory) => void;
    dense?: boolean;
}

interface ExpenseDataGridSize extends TablePropsSizeOverrides {
    size: 'small' | 'medium';
}

export default function ExpensesDataGrid({rows, dense, onClick}: IExpenseDataGridProps) {
    const size: ExpenseDataGridSize = {
        size: dense ? "small" : 'medium'
    }

    return (
        <TableContainer component={Paper}>
            <Table
                sx={{minWidth: 650}}
                size={size.size}
                aria-label={"simple table" + (dense ? "dense" : "")}
            >
                <TableHead sx={{backgroundColor: 'white'}}>
                    <TableRow>
                        <TableCell>Amount</TableCell>
                        <TableCell>Category</TableCell>
                        <TableCell>Merchant</TableCell>
                        <TableCell>Date</TableCell>
                    </TableRow>
                </TableHead>
                <TableBody>
                    {rows.map((row) => (
                        <TableRow
                            key={row.id}
                            sx={{'&:last-child td, &:last-child th': {border: 0}}}
                        >
                            <TableCell>{row.amount}</TableCell>
                            <TableCell>
                                <CategoryChip
                                    id={row.category.id}
                                    name={row.category.name}
                                    priority={row.category.priority}
                                    onClick={() => onClick(row.category)}
                                />
                            </TableCell>
                            <TableCell>{row.merchant}</TableCell>
                            <TableCell>{formatDate(row.date)}</TableCell>
                        </TableRow>
                    ))}
                </TableBody>
            </Table>
        </TableContainer>
    );
}


const formatDate = (date: number) => {
    const formatter = Intl.DateTimeFormat('en-US');
    const result = formatter.format(date);
    return result;
}