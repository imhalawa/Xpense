import { describe, expect, it, vi } from "vitest";
import { Currency } from "../typings/enums/Currency";
import { fixtureProjection } from "./fixtureProjection";
import type { FixtureSeed } from "./fixtureProjection";
import type { TransactionFilter, TransactionView, VaultState } from "./VaultProjection";

const personalSpace = "personal";

const buildTransaction = (
  overrides: Partial<TransactionView> & { id: string },
): TransactionView => ({
  kind: "expense",
  amountMinorUnits: 1000,
  currency: Currency.EUR,
  occurredAt: "2026-08-08T12:00:00.000Z",
  accountId: "account-1",
  counterpartyAccountId: null,
  isCounterpartyPrivate: false,
  categoryId: null,
  merchantId: null,
  tagIds: [],
  canEdit: true,
  ...overrides,
});

const buildSeed = (transactions: TransactionView[]): FixtureSeed => ({
  spaces: [{ id: personalSpace, name: "Personal", kind: "personal", canEdit: true }],
  accounts: {
    [personalSpace]: [
      { id: "account-1", label: "Everyday", currency: Currency.EUR, canEdit: true },
      { id: "account-2", label: "Savings", currency: Currency.EUR, canEdit: true },
    ],
  },
  taxonomy: {
    [personalSpace]: [
      {
        id: "category-1",
        kind: "category",
        label: "Groceries",
        foregroundHex: null,
        backgroundHex: null,
      },
      {
        id: "merchant-1",
        kind: "merchant",
        label: "Albert",
        foregroundHex: null,
        backgroundHex: null,
      },
      { id: "tag-1", kind: "tag", label: "Work", foregroundHex: null, backgroundHex: null },
    ],
  },
  transactions: { [personalSpace]: transactions },
});

const buildFilter = (overrides: Partial<TransactionFilter> = {}): TransactionFilter => ({
  space: personalSpace,
  category: null,
  merchant: null,
  tag: null,
  account: null,
  from: null,
  to: null,
  ...overrides,
});

const wholePage = { offset: 0, limit: 50 };

describe("fixtureProjection", () => {
  it("applies a category and a merchant together with AND", async () => {
    const projection = fixtureProjection(
      buildSeed([
        buildTransaction({ id: "both", categoryId: "category-1", merchantId: "merchant-1" }),
        buildTransaction({ id: "category-only", categoryId: "category-1" }),
        buildTransaction({ id: "merchant-only", merchantId: "merchant-1" }),
      ]),
    );

    const page = await projection.queryTransactions(
      buildFilter({ category: "category-1", merchant: "merchant-1" }),
      wholePage,
    );

    expect(page.rows.map((row) => row.id)).toEqual(["both"]);
    expect(page.totalRows).toBe(1);
  });

  it("reports an unknown tag in removed and keeps the facets the seed knows", async () => {
    const projection = fixtureProjection(
      buildSeed([buildTransaction({ id: "one", categoryId: "category-1" })]),
    );

    const resolution = await projection.resolveFilter(
      buildFilter({ category: "category-1", tag: "tag-999" }),
    );

    expect(resolution.removed).toEqual(["tag"]);
    expect(resolution.filter.tag).toBeNull();
    expect(resolution.filter.category).toBe("category-1");
  });

  it("reports an unknown account and an unknown merchant in removed", async () => {
    const projection = fixtureProjection(buildSeed([buildTransaction({ id: "one" })]));

    const resolution = await projection.resolveFilter(
      buildFilter({ merchant: "merchant-999", account: "account-999" }),
    );

    expect(resolution.removed).toEqual(["merchant", "account"]);
    expect(resolution.filter.merchant).toBeNull();
    expect(resolution.filter.account).toBeNull();
  });

  it("includes the whole of the to date and excludes the day after it", async () => {
    const projection = fixtureProjection(
      buildSeed([
        buildTransaction({ id: "before-from", occurredAt: "2026-08-06T23:59:59.000Z" }),
        buildTransaction({ id: "on-from", occurredAt: "2026-08-07T00:00:00.000Z" }),
        buildTransaction({ id: "late-on-to", occurredAt: "2026-08-08T23:59:59.000Z" }),
        buildTransaction({ id: "day-after-to", occurredAt: "2026-08-09T00:00:00.000Z" }),
      ]),
    );

    const page = await projection.queryTransactions(
      buildFilter({ from: "2026-08-07", to: "2026-08-08" }),
      wholePage,
    );

    expect(page.rows.map((row) => row.id)).toEqual(["late-on-to", "on-from"]);
  });

  it("matches an account filter on either side of a transfer", async () => {
    const projection = fixtureProjection(
      buildSeed([
        buildTransaction({ id: "outgoing", accountId: "account-1" }),
        buildTransaction({
          id: "incoming-transfer",
          kind: "transfer",
          accountId: "account-1",
          counterpartyAccountId: "account-2",
        }),
      ]),
    );

    const page = await projection.queryTransactions(
      buildFilter({ account: "account-2" }),
      wholePage,
    );

    expect(page.rows.map((row) => row.id)).toEqual(["incoming-transfer"]);
  });

  it("sorts newest first and slices by the requested page", async () => {
    const transactions = [...Array(120).keys()].map((index) =>
      buildTransaction({
        id: `transaction-${index}`,
        occurredAt: new Date(Date.UTC(2026, 0, 1 + index)).toISOString(),
      }),
    );
    const projection = fixtureProjection(buildSeed(transactions));

    const page = await projection.queryTransactions(buildFilter(), { offset: 50, limit: 50 });

    expect(page.totalRows).toBe(120);
    expect(page.rows).toHaveLength(50);
    expect(page.rows[0].id).toBe("transaction-69");
    expect(page.rows[49].id).toBe("transaction-20");
  });

  it("rejects every method once locked and reports the locked state", async () => {
    const projection = fixtureProjection(buildSeed([buildTransaction({ id: "one" })]));

    projection.lock();

    expect(projection.state).toBe<VaultState>("locked");
    await expect(projection.listSpaces()).rejects.toThrow();
    await expect(projection.listAccounts(personalSpace)).rejects.toThrow();
    await expect(projection.listTaxonomy(personalSpace, "category")).rejects.toThrow();
    await expect(projection.resolveFilter(buildFilter())).rejects.toThrow();
    await expect(projection.queryTransactions(buildFilter(), wholePage)).rejects.toThrow();
    await expect(
      projection.saveTransaction({
        id: null,
        space: personalSpace,
        kind: "expense",
        amountMinorUnits: 500,
        currency: Currency.EUR,
        occurredAt: "2026-08-08T12:00:00.000Z",
        accountId: "account-1",
        categoryId: null,
        merchantLabel: null,
        tagLabels: [],
        reason: null,
      }),
    ).rejects.toThrow();
  });

  it("notifies subscribers on lock and stops after unsubscribing", async () => {
    const projection = fixtureProjection(buildSeed([buildTransaction({ id: "one" })]));
    const listener = vi.fn();

    const unsubscribe = projection.subscribe(listener);
    projection.lock();

    expect(listener).toHaveBeenCalledWith("locked");

    unsubscribe();
    await projection.unlock();

    expect(listener).toHaveBeenCalledTimes(1);
    expect(projection.state).toBe<VaultState>("ready");
  });

  it("starts ready and lists the seeded spaces, accounts and taxonomy of one kind", async () => {
    const projection = fixtureProjection(buildSeed([buildTransaction({ id: "one" })]));

    expect(projection.state).toBe<VaultState>("ready");
    expect((await projection.listSpaces()).map((space) => space.id)).toEqual([personalSpace]);
    expect((await projection.listAccounts(personalSpace)).map((account) => account.id)).toEqual([
      "account-1",
      "account-2",
    ]);
    expect(
      (await projection.listTaxonomy(personalSpace, "merchant")).map((value) => value.id),
    ).toEqual(["merchant-1"]);
  });

  it("creates, updates, and deletes sidebar resources without reordering their peers", async () => {
    const projection = fixtureProjection(buildSeed([]));
    const account = await projection.createAccount(personalSpace, {
      label: "Cash",
      currency: Currency.EUR,
      openingBalanceMinorUnits: 500,
      isDefault: false,
    });
    const tag = await projection.createTaxonomy(personalSpace, "tag", {
      label: "Holiday",
      backgroundHex: "#EDEDED",
      foregroundHex: "#242424",
    });
    await projection.updateTaxonomy(personalSpace, "tag", tag.id, {
      label: "Travel",
      backgroundHex: "#FFFFFF",
      foregroundHex: "#000000",
    });

    expect((await projection.listAccounts(personalSpace)).map((item) => item.label)).toEqual([
      "Everyday",
      "Savings",
      "Cash",
    ]);
    expect((await projection.listTaxonomy(personalSpace, "tag")).map((item) => item.label)).toEqual([
      "Work",
      "Travel",
    ]);

    await projection.deleteAccount(personalSpace, account.id);
    await projection.deleteTaxonomy(personalSpace, "tag", tag.id);
    expect(await projection.resolveFilter(buildFilter({ account: account.id, tag: tag.id }))).toEqual(
      expect.objectContaining({ removed: ["tag", "account"] }),
    );
  });

  it("adds a saved draft to the space it was written to", async () => {
    const projection = fixtureProjection(buildSeed([buildTransaction({ id: "one" })]));

    const saved = await projection.saveTransaction({
      id: null,
      space: personalSpace,
      kind: "income",
      amountMinorUnits: 2500,
      currency: Currency.EUR,
      occurredAt: "2026-09-01T09:00:00.000Z",
      accountId: "account-1",
      categoryId: "category-1",
      merchantLabel: "Albert",
      tagLabels: ["Work"],
      reason: null,
    });

    expect(saved.amountMinorUnits).toBe(2500);
    expect(saved.merchantId).toBe("merchant-1");
    expect(saved.tagIds).toEqual(["tag-1"]);

    const page = await projection.queryTransactions(buildFilter(), wholePage);

    expect(page.totalRows).toBe(2);
    expect(page.rows[0].id).toBe(saved.id);
  });

  it("replaces an existing transaction when its draft carries the record identifier", async () => {
    const projection = fixtureProjection(
      buildSeed([buildTransaction({ id: "existing", amountMinorUnits: 1000, reason: "Before" })]),
    );

    await projection.saveTransaction({
      id: "existing",
      space: personalSpace,
      kind: "expense",
      amountMinorUnits: 2400,
      currency: Currency.EUR,
      occurredAt: "2026-08-09T10:00:00.000Z",
      accountId: "account-1",
      categoryId: "category-1",
      merchantLabel: "Albert",
      tagLabels: ["Work"],
      reason: "After",
    });

    const page = await projection.queryTransactions(buildFilter(), wholePage);
    expect(page.rows).toHaveLength(1);
    expect(page.rows[0]).toMatchObject({ id: "existing", amountMinorUnits: 2400, reason: "After" });
  });

  it("removes an editable transaction", async () => {
    const projection = fixtureProjection(buildSeed([buildTransaction({ id: "existing" })]));

    await projection.deleteTransaction(personalSpace, "existing");

    await expect(projection.getTransaction(personalSpace, "existing")).resolves.toBeNull();
    await expect(projection.deleteTransaction(personalSpace, "missing")).rejects.toThrow(
      "The transaction was not found",
    );
  });
});
