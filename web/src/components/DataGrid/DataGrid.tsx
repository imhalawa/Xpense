import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Paper from "@mui/material/Paper";
import { Alert, Box, styled, Typography } from "@mui/material";
import { ReactNode } from "react";

export interface IDataGridProps<T> {
  headers: Array<IDataGridHeader<T>>;
  rows: Array<T>;
  dense?: boolean;
  emptyAlert?: ReactNode;
}

export interface IDataGridHeader<T> {
  headerName: string;
  field: keyof T;
  order: number;
  icon?: ReactNode;
  render?: (row: T) => ReactNode;
}

const StyledTableRow = styled(TableRow)(({ theme }) => ({
  //   "&:nth-of-type(odd)": {
  //     backgroundColor: theme.palette.background.default,
  //   },
  "&:hover": {
    backgroundColor: theme.palette.action.hover,
    cursor: "pointer",
  },
  // hide last border
  "&:last-child td, &:last-child th": {
    border: 0,
  },
}));

const DataGrid = <T,>({ headers, rows, dense, emptyAlert }: IDataGridProps<T>): JSX.Element => {
  const sortedHeaders = headers.sort((a, b) => a.order - b.order);
  return (
    <TableContainer component={Paper}>
      <Table
        sx={{ minWidth: 650 }}
        size={dense ? "small" : "medium"}
        aria-label={"simple table" + (dense ? "dense" : "")}
      >
        <TableHead >
          <TableRow sx={{ backgroundColor: "navajowhite" }}>
            {sortedHeaders.map((header) => (
              <TableCell key={header.headerName}>
                <Box display="flex" flexDirection="row" alignItems="center" gap={1}>
                  {header.icon && header.icon}
                  <Typography variant="body2">{header.headerName}</Typography>
                </Box>
              </TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {!rows || rows.length === 0 ? (
            <TableCell colSpan={headers.length}>
              {emptyAlert || (
                <Alert severity="info">
                  <Box display="flex" justifyContent="center">
                    No data
                  </Box>
                </Alert>
              )}
            </TableCell>
          ) : (
            rows.map((row, index) => (
              <StyledTableRow key={Math.random() + index} sx={{ "&:last-child td, &:last-child th": { border: 0 } }}>
                {sortedHeaders.map((header, idx) => (
                  <TableCell key={Math.random() + idx}>
                    {header.render != null ? header.render(row) : (row[header.field] as ReactNode)}
                  </TableCell>
                ))}
              </StyledTableRow>
            ))
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
};

export default DataGrid;
