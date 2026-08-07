import Box from "@mui/material/Box";
import { DollarIcon, EuroIcon } from "../../icons/icons";
import { Currency } from "../../typings/enums/Currency";

interface CurrencyOptionProps {
  currency: Currency;
}

const CurrencyOption = ({ currency }: CurrencyOptionProps) => (
  <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
    {currency === Currency.EUR ? <EuroIcon size={16} /> : <DollarIcon size={16} />}
    {currency}
  </Box>
);

export default CurrencyOption;
