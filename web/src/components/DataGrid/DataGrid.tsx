import Table, { TablePropsSizeOverrides } from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Paper from "@mui/material/Paper";
import { Box, Typography } from "@mui/material";
import { ReactNode } from "react";

export interface IDataGridProps<T> {
  headers: Array<IDataGridHeader<T>>;
  rows: Array<T>;
  dense?: boolean;
}

export interface IDataGridHeader<T> {
  headerName: string;
  field: keyof T;
  order: number;
  icon?: ReactNode;
  render?: (row: T) => ReactNode;
}

interface DataGridSize extends TablePropsSizeOverrides {
  size: "small" | "medium";
}

const DataGrid = <T,>({
  headers,
  rows,
  dense,
}: IDataGridProps<T>): JSX.Element => {
  const size: DataGridSize = {
    size: dense ? "small" : "medium",
  };

  const sortedHeaders = headers.sort(
    (header1, header2) => header1.order - header2.order
  );

  return (
    <TableContainer component={Paper}>
      <Table
        sx={{ minWidth: 650 }}
        size={size.size}
        aria-label={"simple table" + (dense ? "dense" : "")}
      >
        <TableHead sx={{ backgroundColor: "#f5f5f5" }}>
          <TableRow>
            {sortedHeaders.map((header) => (
              <TableCell key={header.headerName}>
                <Box
                  display="flex"
                  flexDirection="row"
                  alignItems="center"
                  gap={1}
                >
                  {header.icon && header.icon}
                  <Typography variant="body2">{header.headerName}</Typography>
                </Box>
              </TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((row, index) => (
            <TableRow
              key={index}
              sx={{ "&:last-child td, &:last-child th": { border: 0 } }}
            >
              {sortedHeaders.map((header, idx) => (
                <TableCell key={idx}>
                  {header.render != null
                    ? header.render(row)
                    : (row[header.field] as ReactNode)}
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
};

export default DataGrid;
