import { makeStyles, tokens } from "@fluentui/react-components";
import { MoneyRegular } from "@fluentui/react-icons";
import { Currency } from "../../typings/enums/Currency";

interface CurrencyOptionProps {
  currency: Currency;
}

const useStyles = makeStyles({
  root: {
    display: "inline-flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalXS,
  },
});

const CurrencyOption = ({ currency }: CurrencyOptionProps) => {
  const styles = useStyles();

  return (
    <span className={styles.root}>
      <MoneyRegular />
      {currency}
    </span>
  );
};

export default CurrencyOption;
