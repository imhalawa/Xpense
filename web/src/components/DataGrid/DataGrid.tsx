import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Paper from "@mui/material/Paper";
import { Alert, Box, Pagination, PaginationItem, styled, Typography } from "@mui/material";
import { ReactNode } from "react";
import { ArrowLeftCircle, ArrowRightCircle } from "lucide-react";

export interface IDataGridProps<T> {
  headers: Array<IDataGridHeader<T>>;
  rows: Array<T>;
  paginated: boolean;
  activePage?: number;
  count?: number;
  onPageChange?: (page: number) => void;
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
  "&:hover": {
    backgroundColor: theme.palette.action.hover,
    cursor: "pointer",
  },
  "&:last-child td, &:last-child th": {
    border: 0,
  },
}));

const DataGrid = <T,>({
  headers,
  rows,
  paginated,
  activePage,
  count,
  onPageChange,
  dense,
  emptyAlert,
}: IDataGridProps<T>): JSX.Element => {
  const isPaginated = paginated && activePage && onPageChange && count !== undefined && count > 0;
  const sortedHeaders = headers.sort((a, b) => a.order - b.order);
  return (
    <Box display="flex" flexDirection="column" gap={1} alignItems={"center"}>
      <TableContainer component={Paper}>
        <Table
          sx={{ minWidth: 650 }}
          size={dense ? "small" : "medium"}
          aria-label={"simple table" + (dense ? "dense" : "")}
        >
          <TableHead>
            <TableRow sx={{ backgroundColor: "#eef2f6" }}>
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
                <StyledTableRow key={index} sx={{ "&:last-child td, &:last-child th": { border: 0 } }}>
                  {sortedHeaders.map((header, idx) => (
                    <TableCell key={idx}>
                      {header.render != null ? header.render(row) : (row[header.field] as ReactNode)}
                    </TableCell>
                  ))}
                </StyledTableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>
      {isPaginated && (
        <Pagination
          count={count}
          page={activePage}
          onChange={(_event, page) => onPageChange(page)}
          renderItem={(item) => (
            <PaginationItem slots={{ previous: ArrowLeftCircle, next: ArrowRightCircle }} {...item} />
          )}
        />
      )}
    </Box>
  );
};

export default DataGrid;
