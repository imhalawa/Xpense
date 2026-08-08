import {
  Button,
  Dropdown,
  Field,
  Input,
  Option,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { DatePicker } from "@fluentui/react-datepicker-compat";
import { ArrowTrendingDownRegular, ArrowTrendingRegular } from "@fluentui/react-icons";
import dayjs from "dayjs";
import { Controller, useForm } from "react-hook-form";
import { yupResolver } from "@hookform/resolvers/yup";
import CurrencyOption from "../../../components/CurrencyOption/CurrencyOption";
import AccountAutoComplete from "../../../components/Forms/AutoComplete/AccountAutoComplete/AccountAutoComplete";
import CategoryAutoComplete from "../../../components/Forms/AutoComplete/CategoryAutoComplete/CategoryAutoComplete";
import MerchantAutoComplete from "../../../components/Forms/AutoComplete/MerchantAutoComplete/MerchantAutoComplete";
import TagAutoComplete from "../../../components/Forms/AutoComplete/TagAutoComplete/TagAutoComplete";
import { Currency } from "../../../typings/enums/Currency";
import { TransactionType } from "../../../typings/enums/TransactionType";
import {
  ITransactionFormData,
  schema,
} from "../../../typings/forms/ITransactionFormData";
import { useLoading } from "../../../contexts/LoadingContext";
import { toMinorUnits } from "../../../typings/models/IMoney";
import type { AccountView, TransactionDraft } from "../../../vault/VaultProjection";
import type { IAccount } from "../../../typings/models/IAccount";

export interface ITransactionFormProps {
  onCancel: () => void;
  onSubmit: (draft: TransactionDraft) => Promise<void>;
  accounts: AccountView[];
  activeSpace: string;
  submitLabel?: string;
}

const currencies: Currency[] = Object.values(Currency);

const useStyles = makeStyles({
  form: {
    display: "flex",
    flexDirection: "column",
    gap: tokens.spacingVerticalL,
    paddingTop: tokens.spacingVerticalS,
  },
  amountRow: {
    display: "grid",
    gridTemplateColumns: "minmax(0, 1fr) 160px",
    gap: tokens.spacingHorizontalM,
  },
  option: {
    display: "flex",
    alignItems: "center",
    gap: tokens.spacingHorizontalS,
  },
  control: {
    minWidth: 0,
    width: "100%",
  },
  income: {
    color: tokens.colorBrandForeground1,
  },
  expense: {
    color: tokens.colorStatusDangerForeground1,
  },
  actions: {
    display: "flex",
    justifyContent: "flex-end",
    gap: tokens.spacingHorizontalS,
  },
});

const TransactionsForm = ({
  onCancel,
  onSubmit,
  accounts,
  activeSpace,
  submitLabel = "Create",
}: ITransactionFormProps) => {
  const styles = useStyles();
  const { setLoading } = useLoading();
  const accountOptions: IAccount[] = accounts.map((account, index) => ({
    accountNumber: account.id,
    label: account.label,
    balance: { minorUnits: 0, currency: account.currency },
    isDefault: index === 0,
  }));

  const { handleSubmit, control } = useForm<ITransactionFormData>({
    resolver: yupResolver(schema),
    defaultValues: {
      amount: 0,
      currency: Currency.EUR,
      type: TransactionType.DEBIT,
      dateOfTransaction: dayjs().unix(),
      account: accountOptions[0] ?? null,
      category: null,
      merchant: null,
      tags: [],
    },
  });

  const submit = async (data: ITransactionFormData) => {
    setLoading(true);
    const amount = toMinorUnits(data.amount, data.currency);
    try {
      await onSubmit({
        id: null,
        space: activeSpace,
        kind: data.type === TransactionType.CREDIT ? "income" : "expense",
        amountMinorUnits: amount.minorUnits,
        currency: amount.currency,
        occurredAt: dayjs.unix(data.dateOfTransaction).toISOString(),
        accountId: data.account!.accountNumber,
        categoryId: data.category?.id == null ? null : String(data.category.id),
        merchantLabel: data.merchant?.label ?? null,
        tagLabels: (data.tags ?? []).map((tag) => tag.label),
        reason: null,
      });
    } catch (submitError) {
      console.error(submitError);
    } finally {
      setLoading(false);
    }
  };

  return (
    <form className={styles.form} onSubmit={handleSubmit(submit)} noValidate>
      <div className={styles.amountRow}>
        <Controller
          name="amount"
          control={control}
          render={({ field: { onChange, value }, fieldState: { error } }) => (
            <Field label="Amount" required validationMessage={error?.message}>
              <Input
                className={styles.control}
                type="number"
                value={String(value)}
                onChange={(_event, data) => onChange(data.value)}
              />
            </Field>
          )}
        />

        <Controller
          name="currency"
          control={control}
          render={({ field: { onChange, value }, fieldState: { error } }) => (
            <Field label="Currency" required validationMessage={error?.message}>
              <Dropdown
                className={styles.control}
                value={value}
                selectedOptions={[value]}
                onOptionSelect={(_event, data) => onChange(data.optionValue)}>
                {currencies.map((currency) => (
                  <Option key={currency} value={currency} text={currency}>
                    <CurrencyOption currency={currency} />
                  </Option>
                ))}
              </Dropdown>
            </Field>
          )}
        />
      </div>

      <Controller
        name="merchant"
        control={control}
        render={({ field: { onChange, value }, fieldState: { error } }) => (
          <MerchantAutoComplete
            label="Merchant"
            value={value}
            error={error !== undefined}
            helperText={error?.message}
            onChange={onChange}
          />
        )}
      />

      <Controller
        name="category"
        control={control}
        render={({ field: { onChange, value }, fieldState: { error } }) => (
          <CategoryAutoComplete
            label="Category"
            value={value}
            error={error !== undefined}
            helperText={error?.message}
            onChange={onChange}
          />
        )}
      />

      <Controller
        name="dateOfTransaction"
        control={control}
        render={({ field: { onChange, value }, fieldState: { error } }) => (
          <Field label="Date of transaction" required validationMessage={error?.message}>
            <DatePicker
              className={styles.control}
              value={dayjs.unix(value).toDate()}
              maxDate={new Date()}
              formatDate={(date) =>
                date === undefined ? "" : dayjs(date).format("YYYY-MM-DD")
              }
              onSelectDate={(date) => onChange(date == null ? null : dayjs(date).unix())}
            />
          </Field>
        )}
      />

      <Controller
        name="type"
        control={control}
        render={({ field: { onChange, value }, fieldState: { error } }) => (
          <Field label="Transaction type" required validationMessage={error?.message}>
            <Dropdown
              className={styles.control}
              value={value === TransactionType.CREDIT ? "Income" : "Expense"}
              selectedOptions={[String(value)]}
              onOptionSelect={(_event, data) =>
                onChange(Number(data.optionValue) as TransactionType)
              }>
              <Option value={String(TransactionType.CREDIT)} text="Income">
                <span className={styles.option}>
                  <ArrowTrendingRegular className={styles.income} /> Income
                </span>
              </Option>
              <Option value={String(TransactionType.DEBIT)} text="Expense">
                <span className={styles.option}>
                  <ArrowTrendingDownRegular className={styles.expense} /> Expense
                </span>
              </Option>
            </Dropdown>
          </Field>
        )}
      />

      <Controller
        name="account"
        control={control}
        render={({ field: { onChange, value }, fieldState: { error } }) => (
          <AccountAutoComplete
            label="Account"
            value={value}
            options={accountOptions}
            error={error !== undefined}
            helperText={error?.message}
            onChange={onChange}
          />
        )}
      />

      <Controller
        name="tags"
        control={control}
        render={({ field: { onChange, value }, fieldState: { error } }) => (
          <TagAutoComplete
            label="Tags"
            value={value}
            error={error !== undefined}
            helperText={error?.message}
            onChange={onChange}
          />
        )}
      />

      <div className={styles.actions}>
        <Button type="button" appearance="secondary" onClick={onCancel}>
          Cancel
        </Button>
        <Button type="submit" appearance="primary">
          {submitLabel}
        </Button>
      </div>
    </form>
  );
};

export default TransactionsForm;
