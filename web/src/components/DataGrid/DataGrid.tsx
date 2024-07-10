import Table, { TablePropsSizeOverrides } from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Paper from "@mui/material/Paper";
import { ITransactionRow } from "../../typings/ITransactionRow.ts";
import CategoryChip from "../Chips/CategoryChip/CategoryChip.tsx";
import { ICategory } from "../../typings/ICategory.ts";
import { formatDate } from "../../utils/DateUtils.ts";
import {
  CalendarDaysIcon,
  CircleAlertIcon,
  CreditCardIcon,
  CurrencyIcon,
  DollarSign,
  Euro,
  FingerprintIcon,
  StoreIcon,
  TagIcon,
} from "lucide-react";
import { TransactionType } from "../../typings/ITransactionType.ts";
import { Currency } from "../../typings/IMoney.ts";
import { Box, Link, styled, Typography } from "@mui/material";

interface IXpenseDataGridProps {
  rows: ITransactionRow[];
  onClick: (category: ICategory) => void;
  dense?: boolean;
}

interface XpenseDataGridSize extends TablePropsSizeOverrides {
  size: "small" | "medium";
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

export default function XpensesDataGrid({
  rows,
  dense,
  onClick,
}: IXpenseDataGridProps) {
  const size: XpenseDataGridSize = {
    size: dense ? "small" : "medium",
  };

  return (
    <TableContainer component={Paper}>
      <Table
        sx={{ minWidth: 650 }}
        size={size.size}
        aria-label={"simple table" + (dense ? "dense" : "")}
      >
        <TableHead sx={{ backgroundColor: "#f5f5f5" }}>
          <TableRow>
            <TableCell>
              <Box display="flex" flexDirection="row" alignItems="center">
                <FingerprintIcon />
                <Typography variant="body2" ml={0.5}>
                  ID
                </Typography>
              </Box>
            </TableCell>
            <TableCell>
              <Box display="flex" flexDirection="row" alignItems="center">
                <CurrencyIcon />
                <Typography variant="body2" ml={0.5}>
                  Amount
                </Typography>
              </Box>
            </TableCell>
            <TableCell>
              <Box display="flex" flexDirection="row" alignItems="center">
                <CalendarDaysIcon />
                <Typography variant="body2" ml={0.5}>
                  Date
                </Typography>
              </Box>
            </TableCell>
            <TableCell>
              <Box display="flex" flexDirection="row" alignItems="center">
                <CircleAlertIcon />
                <Typography variant="body2" ml={0.5}>
                  Category
                </Typography>
              </Box>
            </TableCell>
            <TableCell>
              <Box display="flex" flexDirection="row" alignItems="center">
                <StoreIcon />
                <Typography variant="body2" ml={0.5}>
                  Merchant
                </Typography>
              </Box>
            </TableCell>
            <TableCell>
              <Box display="flex" flexDirection="row" alignItems="center">
                <CreditCardIcon />
                <Typography variant="body2" ml={0.5}>
                  Account
                </Typography>
              </Box>
            </TableCell>
            <TableCell>
              <Box display="flex" flexDirection="row" alignItems="center">
                <TagIcon />
                <Typography variant="body2" ml={0.5}>
                  Tags
                </Typography>
              </Box>
            </TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((row) => (
            <StyledTableRow
              key={row.id}
              sx={{ "&:last-child td, &:last-child th": { border: 0 } }}
            >
              <TableCell>{row.id}</TableCell>
              <TableCell>
                {row.type == TransactionType.CREDIT ? (
                  <Typography variant="body2" color="green">
                    {row.amount.currency == Currency.EUR ? (
                      <Euro size={12} />
                    ) : (
                      <DollarSign size={12} />
                    )}
                    {row.amount.value}
                  </Typography>
                ) : (
                  <Typography variant="body2" color="red">
                    {row.amount.currency == Currency.EUR ? (
                      <Euro size={12} />
                    ) : (
                      <DollarSign size={12} />
                    )}
                    {row.amount.value}
                  </Typography>
                )}
              </TableCell>
              <TableCell>{formatDate(row.date)}</TableCell>
              <TableCell>
                <CategoryChip
                  id={row.category.id}
                  name={row.category.name}
                  priority={row.category.priority}
                  onClick={() => onClick(row.category)}
                />
              </TableCell>
              <TableCell>{row.merchant.name}</TableCell>
              <TableCell>{row.account.accountNumber}</TableCell>
              <TableCell>
                {row.tags &&
                  row.tags.map((tag) => (
                    <Link
                      underline="hover"
                      sx={{
                        mr: 1,
                        "&:hover": {
                          cursor: "pointer",
                        },
                      }}
                      color="darkblue"
                    >
                      #{tag.name}
                    </Link>
                  ))}
              </TableCell>
            </StyledTableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
