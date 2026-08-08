import { describe, expect, it } from "vitest";
import { Currency } from "../../typings/enums/Currency";
import { parseQuickAdd } from "./parser";
import { QuickAddParserContext } from "./types";

const context: QuickAddParserContext = {
  now: new Date("2026-08-09T12:00:00.000Z"),
  supportedCurrencies: [Currency.EUR, Currency.USD],
  defaultAccountId: "cash",
  accounts: [
    { id: "cash", label: "Cash", currency: Currency.EUR },
    { id: "checking", label: "Checking", currency: Currency.EUR },
    { id: "savings", label: "Savings", currency: Currency.EUR },
    { id: "joint", label: "Joint checking", currency: Currency.EUR },
    { id: "rainy", label: "Rainy day", currency: Currency.EUR },
    { id: "dollars", label: "Dollar account", currency: Currency.USD },
  ],
  categories: [
    { id: "shopping", label: "Shopping" },
    { id: "eating-out", label: "Eating out" },
    { id: "entertainment", label: "Entertainment" },
    { id: "salary", label: "Salary" },
    { id: "refunds", label: "Refunds" },
    { id: "freelance", label: "Freelance" },
  ],
  merchants: [
    { id: "albert", label: "Albert Heijn" },
    { id: "cinema", label: "Cinema" },
    { id: "employer", label: "Employer" },
  ],
  tags: [{ id: "family", label: "family" }],
};

const parse = (input: string, overrides: Partial<QuickAddParserContext> = {}) =>
  parseQuickAdd(input, { ...context, ...overrides });

const booleanCombinations = (fieldCount: number) =>
  Array.from({ length: 2 ** fieldCount }, (_, value) =>
    [Array.from({ length: fieldCount }, (__, index) => (value & (1 << index)) !== 0)]
  );

describe("parseQuickAdd accepted combinations", () => {
  it.each([
    ["Spent 5 euros at Albert Heijn #shopping", "expense", 500, Currency.EUR, "Albert Heijn", "Shopping"],
    ["5 Albert Heijn #shopping", "expense", 500, Currency.EUR, "Albert Heijn", "Shopping"],
    ["€5 at Albert Heijn #shopping", "expense", 500, Currency.EUR, "Albert Heijn", "Shopping"],
    ["5,20 EUR at Albert Heijn #shopping", "expense", 520, Currency.EUR, "Albert Heijn", "Shopping"],
    ["paid Albert Heijn 5 EUR #shopping", "expense", 500, Currency.EUR, "Albert Heijn", "Shopping"],
    ["bought coffee for 4.50 #\"Eating out\"", "expense", 450, Currency.EUR, "coffee", "Eating out"],
    ["spent 18 @Cash at Cinema #Entertainment", "expense", 1800, Currency.EUR, "Cinema", "Entertainment"],
    ["spent 18 from Cash at Cinema #Entertainment", "expense", 1800, Currency.EUR, "Cinema", "Entertainment"],
    ["received 2400 EUR from Employer #Salary", "income", 240000, Currency.EUR, "Employer", "Salary"],
    ["refund 12 from Albert Heijn #Refunds >Cash", "income", 1200, Currency.EUR, "Albert Heijn", "Refunds"],
  ] as const)("parses %s", (input, kind, amount, currency, merchant, category) => {
    const result = parse(input);

    expect(result.issues, result.issues.map((issue) => issue.message).join("; ")).toEqual([]);
    expect(result.canSubmit).toBe(true);
    expect(result.draft).toMatchObject({
      kind,
      amountMinorUnits: amount,
      currency,
      merchant: { label: merchant },
      category: { label: category },
    });
  });

  it.each([
    ["spent 18 at Cinema #Entertainment yesterday", "2026-08-08"],
    ["spent 18 at Cinema #Entertainment last Friday", "2026-08-07"],
    ["spent 18 at Cinema #Entertainment 2026-08-05", "2026-08-05"],
    ["spent 18 at Cinema #Entertainment 14:30", "2026-08-09"],
    ["spent 18 at Cinema #Entertainment date:2026-08-05 time:2pm", "2026-08-05"],
  ])("resolves the date in %s", (input, expectedDate) => {
    const result = parse(input);

    expect(result.issues).toEqual([]);
    expect(result.draft.occurredAt?.slice(0, 10)).toBe(expectedDate);
  });

  it("creates unknown merchants and tags inline but not categories", () => {
    const result = parse("spent 18 at New Shop #Shopping ~family ~weekend");

    expect(result.issues).toEqual([]);
    expect(result.draft.merchant).toEqual({ id: "", label: "New Shop", create: true });
    expect(result.draft.tags).toEqual([
      { id: "family", label: "family", create: false },
      { id: "", label: "weekend", create: true },
    ]);
  });

  it.each([
    ["moved 500 from Checking to Savings", null],
    ["transferred 500 @Checking >Savings", null],
    ["500 @Checking >Savings", null],
    ["moved 500 EUR @Checking >Savings yesterday", null],
    ["moved 500 @Checking >Savings reason:\"rent buffer\"", "rent buffer"],
    ["moved 500 @Checking >Savings rent buffer", "rent buffer"],
    ["moved 500 @\"Joint checking\" >\"Rainy day\"", null],
  ])("parses transfer %s", (input, reason) => {
    const result = parse(input);

    expect(result.issues, result.issues.map((issue) => issue.message).join("; ")).toEqual([]);
    expect(result.draft).toMatchObject({ kind: "transfer", amountMinorUnits: 50000, reason });
    expect(result.draft.sourceAccount).not.toBeNull();
    expect(result.draft.destinationAccount).not.toBeNull();
  });

  it("preserves all resolved values when optional tags are added", () => {
    const base = parse("spent 18 at Cinema #Entertainment yesterday");
    const tagged = parse("spent 18 at Cinema #Entertainment yesterday ~family ~weekend");

    expect(tagged.draft).toMatchObject({ ...base.draft, tags: tagged.draft.tags });
    expect(tagged.draft.tags.map((tag) => tag.label)).toEqual(["family", "weekend"]);
  });

  it.each(booleanCombinations(5))(
    "parses the expense optional-field cross-product %j",
    ([withCurrency, withAccount, withDate, withTime, withTag]) => {
      const input = [
        "spent 5",
        withCurrency ? "EUR" : "",
        withAccount ? "@Checking" : "",
        "at Cinema #Entertainment",
        withDate ? "yesterday" : "",
        withTime ? "time:9am" : "",
        withTag ? "~family" : "",
      ].filter(Boolean).join(" ");
      const result = parse(input);

      expect(result.issues, `${input}: ${result.issues.map((issue) => issue.message).join("; ")}`).toEqual([]);
      expect(result.draft.kind).toBe("expense");
      expect(result.draft.amountMinorUnits).toBe(500);
    }
  );

  it.each(booleanCombinations(5))(
    "parses the income optional-field cross-product %j",
    ([withCurrency, withAccount, withDate, withTime, withTag]) => {
      const input = [
        "received 5",
        withCurrency ? "EUR" : "",
        "from Employer #Salary",
        withAccount ? ">Checking" : "",
        withDate ? "yesterday" : "",
        withTime ? "time:9am" : "",
        withTag ? "~family" : "",
      ].filter(Boolean).join(" ");
      const result = parse(input);

      expect(result.issues, `${input}: ${result.issues.map((issue) => issue.message).join("; ")}`).toEqual([]);
      expect(result.draft.kind).toBe("income");
      expect(result.draft.amountMinorUnits).toBe(500);
    }
  );

  it.each(booleanCombinations(5))(
    "parses the transfer optional-field cross-product %j",
    ([withCurrency, withDate, withTime, withTag, withReason]) => {
      const input = [
        "moved 5 @Checking >Savings",
        withCurrency ? "EUR" : "",
        withDate ? "yesterday" : "",
        withTime ? "time:9am" : "",
        withTag ? "~family" : "",
        withReason ? "reason:\"rent buffer\"" : "",
      ].filter(Boolean).join(" ");
      const result = parse(input);

      expect(result.issues, `${input}: ${result.issues.map((issue) => issue.message).join("; ")}`).toEqual([]);
      expect(result.draft.kind).toBe("transfer");
      expect(result.draft.amountMinorUnits).toBe(500);
    }
  );
});

describe("parseQuickAdd rejected and unresolved combinations", () => {
  it.each([
    ["spent at Cinema #Entertainment", "missing-amount"],
    ["spent 0 at Cinema #Entertainment", "invalid-amount"],
    ["spent -5 at Cinema #Entertainment", "invalid-amount"],
    ["spent 5 6 at Cinema #Entertainment", "conflicting-amount"],
    ["spent 5 GBP at Cinema #Entertainment", "unsupported-currency"],
    ["spent 5 at Cinema", "missing-category"],
    ["spent 5 #Entertainment", "missing-merchant"],
    ["spent 5 at Cinema #Unknown", "unknown-category"],
    ["spent 5 at Cinema for Unknown", "unknown-category"],
    ["moved 5 @Checking >Savings #Shopping", "transfer-category"],
    ["moved 5 @Checking >Checking", "same-transfer-account"],
    ["moved 5 @Checking >\"Dollar account\"", "cross-currency-transfer"],
    ["spent 5 at Cinema #Entertainment date:2026-02-30", "invalid-date"],
    ["spent 5 at Cinema #Entertainment 2027-01-01", "future-date"],
    ["received 5 @Cash from Employer #Salary", "kind-account-conflict"],
  ])("reports %s as %s", (input, code) => {
    const result = parse(input);

    expect(result.canSubmit).toBe(false);
    expect(result.issues.map((issue) => issue.code)).toContain(code);
  });

  it("returns account candidates for an ambiguous prefix", () => {
    const result = parse("spent 5 @cas at Cinema #Entertainment", {
      accounts: [
        ...context.accounts,
        { id: "cashback", label: "Cashback", currency: Currency.EUR },
      ],
    });

    expect(result.issues).toContainEqual(
      expect.objectContaining({
        code: "ambiguous-account",
        candidates: ["Cash", "Cashback"],
      })
    );
  });

  it.each([
    ["spent paid 5 at Cinema #Entertainment", "conflicting-kind"],
    ["spent 5 at Cinema #Shopping #Entertainment", "conflicting-category"],
    ["spent 5 USD EUR at Cinema #Entertainment", "conflicting-currency"],
    ["spent 5 at Cinema #Entertainment yesterday today", "conflicting-date"],
  ])("does not use last-write-wins for %s", (input, code) => {
    const result = parse(input);

    expect(result.issues.map((issue) => issue.code)).toContain(code);
    expect(result.ranges.some((range) => range.status === "conflict")).toBe(true);
  });
});

describe("parseQuickAdd decoration ranges", () => {
  it("returns exact, ordered source ranges for Todoist-style feedback", () => {
    const input = "Spent 5 euros at Albert Heijn #shopping ~family";
    const result = parse(input);

    expect(result.ranges.map((range) => input.slice(range.start, range.end))).toEqual([
      "Spent",
      "5",
      "euros",
      "Albert Heijn",
      "#shopping",
      "~family",
    ]);
    expect(result.ranges.every((range) => range.status === "recognized")).toBe(true);
    expect(result.announcement).toContain("0 issues");
  });
});
