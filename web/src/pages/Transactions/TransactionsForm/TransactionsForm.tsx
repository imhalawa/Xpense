import {
  Button,
  Caption1,
  Dropdown,
  Field,
  Input,
  MessageBar,
  MessageBarBody,
  Option,
  makeStyles,
  tokens,
} from "@fluentui/react-components";
import { ArrowTrendingDownRegular, ArrowTrendingRegular } from "@fluentui/react-icons";
import dayjs from "dayjs";
import { Controller, useForm } from "react-hook-form";
import { yupResolver } from "@hookform/resolvers/yup";
import { useCallback, useMemo, useRef, useState } from "react";
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
import type {
  AccountView,
  TransactionDraft,
} from "../../../vault/VaultProjection";
import type { TaxonomyValue, TransactionView } from "../../../vault/VaultProjection";
import type { IAccount } from "../../../typings/models/IAccount";
import type { ICategory } from "../../../typings/models/ICategory";
import type { IMerchant } from "../../../typings/models/IMerchant";
import type { ITag } from "../../../typings/models/ITag";
import { QuickAddInput } from "../../../transactions/quickAdd/QuickAddInput";
import type {
  QuickAddParseResult,
  QuickAddParserContext,
  QuickAddPickerRequest,
  QuickAddField,
  QuickAddCategoryPriority,
} from "../../../transactions/quickAdd/types";

export interface ITransactionFormProps {
  onCancel: () => void;
  onSubmit: (draft: TransactionDraft) => Promise<void>;
  accounts: AccountView[];
  activeSpace: string;
  transaction?: TransactionView;
  taxonomy?: TaxonomyValue[];
  onCreateCategory?: (
    label: string,
    priority: QuickAddCategoryPriority,
  ) => Promise<{ id: string; label: string }>;
  submitLabel?: string;
}

const currencies: Currency[] = Object.values(Currency);
const categoryPriorities: QuickAddCategoryPriority[] = ["Low", "Medium", "High"];
const momentFormat = "YYYY-MM-DDTHH:mm";

interface PendingCategory {
  label: string;
  priority: QuickAddCategoryPriority;
}

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
  parseIssue: {
    marginTop: `-${tokens.spacingVerticalS}`,
  },
});

const TransactionsForm = ({
  onCancel,
  onSubmit,
  accounts,
  activeSpace,
  transaction,
  taxonomy = [],
  onCreateCategory,
  submitLabel = "Create",
}: ITransactionFormProps) => {
  const styles = useStyles();
  const { setLoading } = useLoading();
  const accountOptions: IAccount[] = useMemo(
    () => accounts.map((account, index) => ({
      accountNumber: account.id,
      label: account.label,
      balance: { minorUnits: 0, currency: account.currency },
      isDefault: index === 0,
    })),
    [accounts],
  );
  const taxonomyById = useMemo(() => new Map(taxonomy.map((value) => [value.id, value])), [taxonomy]);
  const category = transaction?.categoryId === null || transaction === undefined
    ? null
    : ({
        id: Number(transaction.categoryId),
        label: taxonomyById.get(transaction.categoryId)?.label ?? transaction.categoryId,
        priority: { id: 0, label: "Medium", weight: 0, createdAt: null, updatedAt: null },
        createdAt: null,
        updatedAt: null,
      } satisfies ICategory);
  const merchant = transaction?.merchantId === null || transaction === undefined
    ? null
    : ({
        id: Number(transaction.merchantId),
        label: taxonomyById.get(transaction.merchantId)?.label ?? transaction.merchantId,
        create: false,
      } satisfies IMerchant);
  const tags = transaction?.tagIds.map((id) => ({
    id: Number(id),
    label: taxonomyById.get(id)?.label ?? id,
    create: false,
    createdAt: null,
    updatedAt: null,
    fgColorHex: null,
    bgColorHex: null,
  } satisfies ITag)) ?? [];

  const quickAddContext = useMemo<QuickAddParserContext>(
    () => ({
      accounts: accounts.map((account) => ({
        id: account.id,
        label: account.label,
        currency: account.currency,
      })),
      categories: taxonomy
        .filter((value) => value.kind === "category")
        .map((value) => ({ id: value.id, label: value.label })),
      merchants: taxonomy
        .filter((value) => value.kind === "merchant")
        .map((value) => ({ id: value.id, label: value.label })),
      tags: taxonomy
        .filter((value) => value.kind === "tag")
        .map((value) => ({ id: value.id, label: value.label })),
      defaultAccountId: accounts[0]?.id,
      supportedCurrencies: currencies,
    }),
    [accounts, taxonomy],
  );
  const [quickAddValue, setQuickAddValue] = useState("");
  const [latestParseResult, setLatestParseResult] = useState<QuickAddParseResult | null>(null);
  const [pendingCategory, setPendingCategory] = useState<PendingCategory | null>(null);
  const [createdCategory, setCreatedCategory] = useState<{
    id: string;
    label: string;
    priority: QuickAddCategoryPriority;
  } | null>(null);
  const [activePickerRequest, setActivePickerRequest] = useState<QuickAddPickerRequest | null>(null);
  const [submitFailure, setSubmitFailure] = useState<string | null>(null);
  const formReference = useRef<HTMLFormElement>(null);

  const { handleSubmit, control, setValue, trigger, watch } = useForm<ITransactionFormData>({
    resolver: yupResolver(schema),
    defaultValues: {
      amount: transaction === undefined ? 0 : transaction.amountMinorUnits / 100,
      currency: transaction?.currency ?? Currency.EUR,
      type:
        transaction?.kind === "income"
          ? TransactionType.CREDIT
          : transaction?.kind === "transfer"
            ? TransactionType.TRANSFER
            : TransactionType.DEBIT,
      dateOfTransaction: transaction === undefined ? dayjs().unix() : dayjs(transaction.occurredAt).unix(),
      account:
        accountOptions.find((account) => account.accountNumber === transaction?.accountId) ??
        accountOptions[0] ??
        null,
      counterpartyAccount:
        accountOptions.find((account) => account.accountNumber === transaction?.counterpartyAccountId) ??
        null,
      category,
      merchant,
      tags,
      reason: transaction?.reason ?? null,
    },
  });
  const selectedType = watch("type");
  const selectedAccount = watch("account");

  const toAccount = useCallback(
    (id: string | null): IAccount | null =>
      accountOptions.find((account) => account.accountNumber === id) ?? null,
    [accountOptions],
  );

  const rewriteActiveRange = (field: QuickAddField, replacement: string) => {
    if (activePickerRequest?.range.field !== field) return;
    activePickerRequest.rewrite(replacement);
    setActivePickerRequest(null);
  };

  const quoted = (field: string, value: string): string =>
    `${field}:"${value.replace(/"/g, "")}"`;

  const focusPicker = (request: QuickAddPickerRequest) => {
    setActivePickerRequest(request);
    const control = request.range.field === "time" ? "date" : request.range.field;
    window.setTimeout(() => {
      const container = formReference.current?.querySelector<HTMLElement>(
        `[data-quick-add-control="${control}"]`,
      );
      container?.querySelector<HTMLElement>("input, button, [role=combobox]")?.focus();
    }, 0);
  };

  const selectType = useCallback((type: TransactionType) => {
    setValue("type", type);
    if (type !== TransactionType.TRANSFER) return;
    setValue("category", null);
    setValue("merchant", null);
    setPendingCategory(null);
    setCreatedCategory(null);
  }, [setValue]);

  const applyQuickAdd = useCallback((result: QuickAddParseResult) => {
    setLatestParseResult(result);
    setSubmitFailure(null);
    const { draft } = result;
    if (draft.kind !== null) {
      selectType(
        draft.kind === "income"
          ? TransactionType.CREDIT
          : draft.kind === "transfer"
            ? TransactionType.TRANSFER
            : TransactionType.DEBIT,
      );
    }
    if (draft.amountMinorUnits !== null) setValue("amount", draft.amountMinorUnits / 100);
    if (draft.currency !== null) setValue("currency", draft.currency);
    if (draft.occurredAt !== null) {
      setValue("dateOfTransaction", dayjs(draft.occurredAt).unix());
    }
    const primaryAccount = draft.kind === "income" ? draft.destinationAccount : draft.sourceAccount;
    if (primaryAccount !== null) setValue("account", toAccount(primaryAccount.id));
    if (draft.destinationAccount !== null && draft.kind === "transfer") {
      setValue("counterpartyAccount", toAccount(draft.destinationAccount.id));
    } else if (result.canSubmit) {
      setValue("counterpartyAccount", null);
    }

    if (draft.kind === "transfer") {
      setValue("merchant", null);
      setValue("category", null);
      setPendingCategory(null);
      setCreatedCategory(null);
    } else {
      if (draft.merchant !== null) {
        setValue("merchant", {
          id: draft.merchant.create ? null : Number(draft.merchant.id),
          label: draft.merchant.label,
          create: draft.merchant.create,
        });
      } else if (result.canSubmit) {
        setValue("merchant", null);
      }

      if (draft.category?.create) {
        const priority = draft.category.priority ?? "Medium";
        setPendingCategory({ label: draft.category.label, priority });
        setCreatedCategory((current) =>
          current?.label === draft.category?.label && current?.priority === priority
            ? current
            : null,
        );
        setValue("category", {
          id: 0,
          label: draft.category.label,
          priority: { id: 0, label: priority, weight: 0, createdAt: null, updatedAt: null },
          createdAt: null,
          updatedAt: null,
        });
      } else if (draft.category !== null) {
        setPendingCategory(null);
        setCreatedCategory(null);
        setValue("category", {
          id: Number(draft.category.id),
          label: draft.category.label,
          priority: { id: 0, label: "Medium", weight: 0, createdAt: null, updatedAt: null },
          createdAt: null,
          updatedAt: null,
        });
      } else if (result.canSubmit) {
        setPendingCategory(null);
        setCreatedCategory(null);
        setValue("category", null);
      }
    }

    if (draft.tags.length > 0 || result.canSubmit) {
      setValue(
        "tags",
        draft.tags.map((tag) => ({
          id: tag.create ? null : Number(tag.id),
          label: tag.label,
          create: tag.create,
          createdAt: null,
          updatedAt: null,
          fgColorHex: null,
          bgColorHex: null,
        })),
      );
    }
    if (draft.reason !== null || result.canSubmit) setValue("reason", draft.reason);
  }, [selectType, setValue, toAccount]);

  const submit = async (data: ITransactionFormData) => {
    if (
      quickAddValue.trim().length > 0 &&
      latestParseResult !== null &&
      !latestParseResult.canSubmit
    ) {
      setSubmitFailure(
        latestParseResult.issues[0]?.message ?? "Resolve the Quick Add text before saving.",
      );
      return;
    }
    if (pendingCategory !== null && onCreateCategory === undefined) {
      setSubmitFailure(`Category “${pendingCategory.label}” must be created before saving.`);
      return;
    }

    setLoading(true);
    setSubmitFailure(null);
    const amount = toMinorUnits(data.amount, data.currency);
    try {
      let categoryForTransaction = createdCategory;
      if (pendingCategory !== null && categoryForTransaction === null) {
        const created = await onCreateCategory!(
          pendingCategory.label,
          pendingCategory.priority,
        );
        categoryForTransaction = {
          id: created.id,
          label: pendingCategory.label,
          priority: pendingCategory.priority,
        };
        setCreatedCategory(categoryForTransaction);
      }
      const isTransfer = data.type === TransactionType.TRANSFER;
      await onSubmit({
        id: transaction?.id ?? null,
        space: activeSpace,
        kind:
          data.type === TransactionType.CREDIT
            ? "income"
            : isTransfer
              ? "transfer"
              : "expense",
        amountMinorUnits: amount.minorUnits,
        currency: amount.currency,
        occurredAt: dayjs.unix(data.dateOfTransaction).toISOString(),
        accountId: data.account!.accountNumber,
        counterpartyAccountId: isTransfer
          ? data.counterpartyAccount?.accountNumber ?? null
          : null,
        categoryId: isTransfer
          ? null
          : categoryForTransaction === null
            ? data.category?.id == null
              ? null
              : String(data.category.id)
            : categoryForTransaction.id,
        merchantLabel: isTransfer ? null : data.merchant?.label ?? null,
        tagLabels: (data.tags ?? []).map((tag) => tag.label),
        reason: isTransfer ? data.reason?.trim() || null : null,
      });
    } catch (submitError) {
      setSubmitFailure(
        submitError instanceof Error
          ? submitError.message
          : "The transaction could not be saved. Try again.",
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <form ref={formReference} className={styles.form} onSubmit={handleSubmit(submit)} noValidate>
      <QuickAddInput
        value={quickAddValue}
        context={quickAddContext}
        onValueChange={(value) => {
          setQuickAddValue(value);
          setSubmitFailure(null);
        }}
        onParseResult={applyQuickAdd}
        onOpenPicker={focusPicker}
      />
      {quickAddValue.trim().length > 0 && latestParseResult !== null && !latestParseResult.canSubmit && (
        <MessageBar className={styles.parseIssue} intent="warning">
          <MessageBarBody>
            {latestParseResult.issues[0]?.message ?? "Resolve the Quick Add text before saving."}
          </MessageBarBody>
        </MessageBar>
      )}
      {pendingCategory !== null && (
        <div data-quick-add-control="category">
          <Caption1 role="status">
            {createdCategory === null
              ? `Category “${pendingCategory.label}” will be created before this transaction is saved.`
              : `Category “${pendingCategory.label}” was created and will be reused when you retry.`}
          </Caption1>
          <Field label="New category priority">
            <Dropdown
              value={pendingCategory.priority}
              selectedOptions={[pendingCategory.priority]}
              disabled={createdCategory !== null}
              onOptionSelect={(_event, data) => {
                const priority = data.optionValue as QuickAddCategoryPriority | undefined;
                if (priority !== undefined) {
                  setPendingCategory((current) =>
                    current === null ? null : { ...current, priority },
                  );
                  setCreatedCategory(null);
                }
              }}>
              {categoryPriorities.map((priority) => (
                <Option key={priority} value={priority} text={priority}>
                  {priority}
                </Option>
              ))}
            </Dropdown>
          </Field>
        </div>
      )}
      {submitFailure !== null && (
        <MessageBar role="alert" intent="error">
          <MessageBarBody>{submitFailure}</MessageBarBody>
        </MessageBar>
      )}
      <div className={styles.amountRow}>
        <div data-quick-add-control="amount">
          <Controller
            name="amount"
            control={control}
            render={({ field: { onChange, value }, fieldState: { error } }) => (
              <Field label="Amount" required validationMessage={error?.message}>
                <Input
                  className={styles.control}
                  type="number"
                  value={String(value)}
                  onChange={(_event, data) => {
                    onChange(data.value);
                    rewriteActiveRange("amount", data.value);
                  }}
                />
              </Field>
            )}
          />
        </div>

        <div data-quick-add-control="currency">
          <Controller
            name="currency"
            control={control}
            render={({ field: { onChange, value }, fieldState: { error } }) => (
              <Field label="Currency" required validationMessage={error?.message}>
                <Dropdown
                  className={styles.control}
                  value={value}
                  selectedOptions={[value]}
                  onOptionSelect={(_event, data) => {
                    onChange(data.optionValue);
                    if (data.optionValue !== undefined) {
                      rewriteActiveRange("currency", data.optionValue);
                    }
                  }}>
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
      </div>

      <div data-quick-add-control="kind"><Controller
        name="type"
        control={control}
        render={({ field: { onChange, value }, fieldState: { error } }) => (
          <Field label="Transaction type" required validationMessage={error?.message}>
            <Dropdown
              className={styles.control}
              value={
                value === TransactionType.CREDIT
                  ? "Income"
                  : value === TransactionType.TRANSFER
                    ? "Transfer"
                    : "Expense"
              }
              selectedOptions={[String(value)]}
              onOptionSelect={(_event, data) => {
                const type = Number(data.optionValue) as TransactionType;
                onChange(type);
                selectType(type);
                rewriteActiveRange(
                  "kind",
                  type === TransactionType.CREDIT
                    ? "received"
                    : type === TransactionType.TRANSFER
                      ? "moved"
                      : "spent",
                );
              }}>
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
              <Option value={String(TransactionType.TRANSFER)} text="Transfer">Transfer</Option>
            </Dropdown>
          </Field>
        )}
      /></div>

      {selectedType !== TransactionType.TRANSFER && (
        <div data-quick-add-control="merchant">
          <Controller
            name="merchant"
            control={control}
            render={({ field: { onChange, value }, fieldState: { error } }) => (
              <MerchantAutoComplete
                label="Merchant"
                value={value}
                error={error !== undefined}
                helperText={error?.message}
                onChange={(next) => {
                  onChange(next);
                  if (next !== null) {
                    rewriteActiveRange("merchant", quoted("merchant", next.label));
                  }
                }}
              />
            )}
          />
        </div>
      )}

      {selectedType !== TransactionType.TRANSFER && pendingCategory === null && (
        <div data-quick-add-control="category">
          <Controller
            name="category"
            control={control}
            render={({ field: { onChange, value }, fieldState: { error } }) => (
              <CategoryAutoComplete
                label="Category"
                value={value}
                error={error !== undefined}
                helperText={error?.message}
                onChange={(next) => {
                  onChange(next);
                  if (next !== null) {
                    rewriteActiveRange("category", quoted("category", next.label));
                  }
                }}
              />
            )}
          />
        </div>
      )}

      <div data-quick-add-control="date"><Controller
        name="dateOfTransaction"
        control={control}
        render={({ field: { onChange, value }, fieldState: { error } }) => (
          <Field label="Date and time of transaction" required validationMessage={error?.message}>
            <Input
              className={styles.control}
              type="datetime-local"
              max={dayjs().format(momentFormat)}
              value={dayjs.unix(value).format(momentFormat)}
              onChange={(_event, data) => {
                const occurredAt = dayjs(data.value).second(0);
                if (!occurredAt.isValid()) return;
                onChange(occurredAt.unix());
                rewriteActiveRange("date", `date:${occurredAt.format("YYYY-MM-DD")}`);
                rewriteActiveRange("time", `time:${occurredAt.format("HH:mm")}`);
              }}
            />
          </Field>
        )}
      /></div>

      <div
        data-quick-add-control={
          selectedType === TransactionType.CREDIT ? "destinationAccount" : "sourceAccount"
        }><Controller
        name="account"
        control={control}
        render={({ field: { onChange, value }, fieldState: { error } }) => (
          <AccountAutoComplete
            label="Account"
            value={value}
            options={accountOptions}
            error={error !== undefined}
            helperText={error?.message}
            onChange={(next) => {
              onChange(next);
              window.queueMicrotask(() => void trigger("counterpartyAccount"));
              if (next !== null) {
                const accountField =
                  selectedType === TransactionType.CREDIT
                    ? "destinationAccount"
                    : "sourceAccount";
                rewriteActiveRange(
                  accountField,
                  quoted(
                    selectedType === TransactionType.CREDIT ? "destination" : "source",
                    next.label,
                  ),
                );
              }
            }}
          />
        )}
      /></div>

      {selectedType === TransactionType.TRANSFER && (
        <div data-quick-add-control="destinationAccount">
          <Controller
            name="counterpartyAccount"
            control={control}
            render={({ field: { onChange, value }, fieldState: { error } }) => (
              <AccountAutoComplete
                label="Destination account"
                value={value}
                options={accountOptions.filter(
                  (account) => account.accountNumber !== selectedAccount?.accountNumber,
                )}
                error={error !== undefined}
                helperText={error?.message}
                onChange={(next) => {
                  onChange(next);
                  window.queueMicrotask(() => void trigger("counterpartyAccount"));
                  if (next !== null) {
                    rewriteActiveRange("destinationAccount", quoted("destination", next.label));
                  }
                }}
              />
            )}
          />
        </div>
      )}

      <div data-quick-add-control="tag"><Controller
        name="tags"
        control={control}
        render={({ field: { onChange, value }, fieldState: { error } }) => (
          <TagAutoComplete
            label="Tags"
            value={value}
            error={error !== undefined}
            helperText={error?.message}
            onChange={(next) => {
              onChange(next);
              const chosen = next?.[next.length - 1];
              if (chosen !== undefined) rewriteActiveRange("tag", quoted("tag", chosen.label));
            }}
          />
        )}
      /></div>

      {selectedType === TransactionType.TRANSFER && (
        <div data-quick-add-control="reason">
          <Controller
            name="reason"
            control={control}
            render={({ field: { onChange, value } }) => (
              <Field label="Note">
                <Input
                  className={styles.control}
                  value={value ?? ""}
                  onChange={(_event, data) => {
                    onChange(data.value || null);
                    rewriteActiveRange("reason", quoted("reason", data.value));
                  }}
                />
              </Field>
            )}
          />
        </div>
      )}

      <div className={styles.actions}>
        <Button type="button" appearance="secondary" onClick={onCancel}>
          Cancel
        </Button>
        <Button
          type="submit"
          appearance="primary"
          disabled={
            quickAddValue.trim().length > 0 &&
            latestParseResult !== null &&
            !latestParseResult.canSubmit
          }>
          {submitLabel}
        </Button>
      </div>
    </form>
  );
};

export default TransactionsForm;
