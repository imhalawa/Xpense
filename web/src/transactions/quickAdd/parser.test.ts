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
    ["purchased 5 at Cinema #Entertainment", "expense"],
    ["charged 5 at Cinema #Entertainment", "expense"],
    ["withdrew 5 at Cinema #Entertainment", "expense"],
    ["earned 2400 from Employer #Salary >Checking", "income"],
    ["was paid 2400 from Employer #Salary >Checking", "income"],
    ["sent 500 from Checking to Savings", "transfer"],
  ] as const)("recognises the natural kind in %s", (input, kind) => {
    const result = parse(input);

    expect(result.issues, result.issues.map((issue) => issue.message).join("; ")).toEqual([]);
    expect(result.draft.kind).toBe(kind);
  });

  it("parses deposited dollar income with its dollar account", () => {
    const result = parse("deposited $100 from Client #Freelance yesterday", {
      defaultAccountId: "dollars",
    });

    expect(result.issues).toEqual([]);
    expect(result.draft).toMatchObject({
      kind: "income",
      amountMinorUnits: 10000,
      currency: Currency.USD,
      destinationAccount: { id: "dollars" },
      merchant: { label: "Client", create: true },
      category: { id: "freelance", create: false },
    });
    expect(result.draft.occurredAt?.slice(0, 10)).toBe("2026-08-08");
  });

  it.each(["dollar", "dollars", "USD"])("recognises the dollar alias %s", (currency) => {
    const result = parse(`spent 5 ${currency} @\"Dollar account\" at Cinema #Entertainment`);

    expect(result.issues).toEqual([]);
    expect(result.draft.currency).toBe(Currency.USD);
  });

  it("resolves an income destination from a natural to phrase", () => {
    const result = parse("received 5 to Checking from Employer #Salary");

    expect(result.issues).toEqual([]);
    expect(result.draft.destinationAccount?.id).toBe("checking");
    expect(result.draft.merchant?.label).toBe("Employer");
  });

  it("resolves a natural category only on an exact known match", () => {
    const result = parse("spent 5 at Cinema for Entertainment");

    expect(result.issues).toEqual([]);
    expect(result.draft.merchant).toEqual({ id: "cinema", label: "Cinema", create: false });
    expect(result.draft.category).toEqual({
      id: "entertainment",
      label: "Entertainment",
      create: false,
    });
    expect(result.draft.category).not.toHaveProperty("priority");
  });

  it.each([
    ["spent 18 at Cinema #Entertainment today", "2026-08-09"],
    ["spent 18 at Cinema #Entertainment yesterday", "2026-08-08"],
    ["spent 18 at Cinema #Entertainment last Friday", "2026-08-07"],
    ["spent 18 at Cinema #Entertainment mon", "2026-08-03"],
    ["spent 18 at Cinema #Entertainment Wednesday", "2026-08-05"],
    ["spent 18 at Cinema #Entertainment Sunday", "2026-08-09"],
    ["spent 18 at Cinema #Entertainment last Sunday", "2026-08-02"],
    ["spent 18 at Cinema #Entertainment 5 Aug", "2026-08-05"],
    ["spent 18 at Cinema #Entertainment Aug 5", "2026-08-05"],
    ["spent 18 at Cinema #Entertainment 2026-08-05", "2026-08-05"],
    ["spent 18 at Cinema #Entertainment 14:30", "2026-08-09"],
    ["spent 18 at Cinema #Entertainment date:2026-08-05 time:2pm", "2026-08-05"],
  ])("resolves the date in %s", (input, expectedDate) => {
    const result = parse(input);

    expect(result.issues).toEqual([]);
    expect(result.draft.occurredAt?.slice(0, 10)).toBe(expectedDate);
  });

  it.each([
    ["this morning", 9],
    ["this afternoon", 15],
    ["this evening", 19],
    ["2pm", 14],
    ["2:15am", 2],
    ["14:30", 14],
  ])("resolves natural time %s", (time, expectedHour) => {
    const result = parse(`spent 18 at Cinema #Entertainment ${time}`);

    expect(result.issues).toEqual([]);
    const occurredAt = new Date(result.draft.occurredAt ?? "");
    expect(occurredAt.getHours()).toBe(expectedHour);
    if (time === "2:15am" || time === "14:30") expect(occurredAt.getMinutes()).toBe(time.endsWith("30") ? 30 : 15);
  });

  it("creates unknown merchants and tags inline", () => {
    const result = parse("spent 18 at New Shop #Shopping ~family ~weekend");

    expect(result.issues).toEqual([]);
    expect(result.draft.merchant).toEqual({ id: "", label: "New Shop", create: true });
    expect(result.draft.tags).toEqual([
      { id: "family", label: "family", create: false },
      { id: "", label: "weekend", create: true },
    ]);
  });

  it("keeps quoted multi-word tags as one created value", () => {
    const result = parse("spent 18 at Cinema #Entertainment ~\"family trip\"");

    expect(result.issues).toEqual([]);
    expect(result.draft.tags).toEqual([{ id: "", label: "family trip", create: true }]);
    expect(result.ranges.find((range) => range.field === "tag")?.text).toBe("~\"family trip\"");
  });

  it("resolves unique account prefixes against labels and ids", () => {
    const result = parse("spent 18 @Che at Cinema #Entertainment");

    expect(result.issues).toEqual([]);
    expect(result.draft.sourceAccount?.id).toBe("checking");
  });

  it.each([
    "spent 18 at New Shop #NewCategory",
    "spent 18 merchant:\"New Shop\" category:\"New Category\"",
  ])("creates an unknown explicit category with editable Medium priority in %s", (input) => {
    const result = parse(input);

    expect(result.issues).toEqual([]);
    expect(result.canSubmit).toBe(true);
    expect(result.draft.category).toEqual({
      id: "",
      label: input.includes("#") ? "NewCategory" : "New Category",
      create: true,
      priority: "Medium",
    });
    expect(result.ranges.find((range) => range.field === "category")?.label).toContain("priority Medium");
  });

  it.each(["eur", "EuR", "EURO", "Euros", "usd", "UsD"])(
    "recognises supported currency %s case-insensitively",
    (currency) => {
      const isEuro = currency.toLocaleLowerCase().startsWith("e");
      const result = parse(
        `spent 5 ${currency} ${isEuro ? "" : "@\"Dollar account\""} at Cinema #Entertainment`
      );

      expect(result.issues).toEqual([]);
      expect(result.draft.currency).toBe(isEuro ? Currency.EUR : Currency.USD);
    }
  );

  it.each([
    ["moved 500 from Checking to Savings", null],
    ["transferred 500 @Checking >Savings", null],
    ["500 @Checking >Savings", null],
    ["moved 500 EUR @Checking >Savings yesterday", null],
    ["moved 500 @Checking >Savings reason:\"rent buffer\"", "rent buffer"],
    ["moved 500 @Checking >Savings rent buffer", "rent buffer"],
    ["moved 500 @\"Joint checking\" >\"Rainy day\"", null],
    ["moved 500 @Checking >Savings ~monthly ~saving", null],
  ])("parses transfer %s", (input, reason) => {
    const result = parse(input);

    expect(result.issues, result.issues.map((issue) => issue.message).join("; ")).toEqual([]);
    expect(result.draft).toMatchObject({ kind: "transfer", amountMinorUnits: 50000, reason });
    expect(result.draft.sourceAccount).not.toBeNull();
    expect(result.draft.destinationAccount).not.toBeNull();
    if (input.includes("~monthly")) {
      expect(result.draft.tags.map((tag) => tag.label)).toEqual(["monthly", "saving"]);
    }
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
      expect(result.draft).toMatchObject({
        kind: "expense",
        amountMinorUnits: 500,
        currency: Currency.EUR,
        sourceAccount: { id: withAccount ? "checking" : "cash" },
        destinationAccount: null,
        merchant: { id: "cinema", label: "Cinema", create: false },
        category: { id: "entertainment", label: "Entertainment", create: false },
      });
      expect(result.draft.tags.map((tag) => tag.label)).toEqual(withTag ? ["family"] : []);
      expect(new Date(result.draft.occurredAt ?? "").getDate()).toBe(withDate ? 8 : 9);
      if (withTime) expect(new Date(result.draft.occurredAt ?? "").getHours()).toBe(9);
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
      expect(result.draft).toMatchObject({
        kind: "income",
        amountMinorUnits: 500,
        currency: Currency.EUR,
        sourceAccount: null,
        destinationAccount: { id: withAccount ? "checking" : "cash" },
        merchant: { id: "employer", label: "Employer", create: false },
        category: { id: "salary", label: "Salary", create: false },
      });
      expect(result.draft.tags.map((tag) => tag.label)).toEqual(withTag ? ["family"] : []);
      expect(new Date(result.draft.occurredAt ?? "").getDate()).toBe(withDate ? 8 : 9);
      if (withTime) expect(new Date(result.draft.occurredAt ?? "").getHours()).toBe(9);
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
      expect(result.draft).toMatchObject({
        kind: "transfer",
        amountMinorUnits: 500,
        currency: Currency.EUR,
        sourceAccount: { id: "checking" },
        destinationAccount: { id: "savings" },
        merchant: null,
        category: null,
        reason: withReason ? "rent buffer" : null,
      });
      expect(result.draft.tags.map((tag) => tag.label)).toEqual(withTag ? ["family"] : []);
      expect(new Date(result.draft.occurredAt ?? "").getDate()).toBe(withDate ? 8 : 9);
      if (withTime) expect(new Date(result.draft.occurredAt ?? "").getHours()).toBe(9);
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
    ["spent 5 gbp at Cinema #Entertainment", "unsupported-currency"],
    ["spent 5 GbP at Cinema #Entertainment", "unsupported-currency"],
    ["spent 5 at Cinema", "missing-category"],
    ["spent 5 #Entertainment", "missing-merchant"],
    ["spent 5 at Cinema for Unknown", "unknown-category"],
    ["moved 5 @Checking >Savings #Shopping", "transfer-category"],
    ["moved 5 @Checking >Savings for Shopping", "transfer-category"],
    ["moved 5 @Checking >Savings at Cinema", "transfer-merchant"],
    ["spent 5 at Cinema #Entertainment reason:\"not allowed\"", "non-transfer-reason"],
    ["received 5 from Employer #Salary reason:\"not allowed\"", "non-transfer-reason"],
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

  it("keeps an unknown account unresolved with an exact source range", () => {
    const input = "spent 5 @Unknown at Cinema #Entertainment";
    const result = parse(input);
    const range = result.ranges.find((candidate) => candidate.field === "sourceAccount");

    expect(result.issues.map((issue) => issue.code)).toContain("unknown-account");
    expect(range).toMatchObject({
      start: input.indexOf("@Unknown"),
      end: input.indexOf("@Unknown") + "@Unknown".length,
      text: "@Unknown",
      status: "unresolved",
    });
  });

  it.each([
    "spent 5 merchant:\"Cinema\" #Entertainment extra words",
    "moved 5 @Checking >Savings reason:\"buffer\" extra words",
  ])("never silently discards text after all supported fields resolve in %s", (input) => {
    const result = parse(input);
    const unresolved = result.ranges.find((range) => range.field === "text");

    expect(result.issues.map((issue) => issue.code)).toContain("unresolved-text");
    expect(unresolved).toMatchObject({
      start: input.indexOf("extra words"),
      end: input.indexOf("extra words") + "extra words".length,
      text: "extra words",
      status: "unresolved",
    });
  });

  it.each(["gbp", "GbP", "GBP"])(
    "keeps unsupported ISO currency %s out of merchant text and does not apply the account default",
    (currency) => {
      const result = parse(`spent 5 ${currency} at Cinema #Entertainment`);

      expect(result.draft.currency).toBeNull();
      expect(result.draft.merchant?.label).toBe("Cinema");
      expect(result.issues.map((issue) => issue.code)).toEqual(["unsupported-currency"]);
    }
  );

  it.each([
    ["spent paid 5 at Cinema #Entertainment", "conflicting-kind"],
    ["spent 5 at Cinema #Shopping #Entertainment", "conflicting-category"],
    ["spent 5 USD EUR at Cinema #Entertainment", "conflicting-currency"],
    ["spent 5 at Cinema #Entertainment yesterday today", "conflicting-date"],
    ["spent 5 @Cash @Checking at Cinema #Entertainment", "conflicting-sourceAccount"],
    ["received 5 from Employer #Salary >Cash >Checking", "conflicting-destinationAccount"],
    ["spent 5 at Cinema #Entertainment time:9am time:10am", "conflicting-time"],
    ["moved 5 @Checking >Savings reason:\"one\" reason:\"two\"", "conflicting-reason"],
  ])("does not use last-write-wins for %s", (input, code) => {
    const result = parse(input);

    expect(result.issues.map((issue) => issue.code)).toContain(code);
    expect(result.ranges.some((range) => range.status === "conflict")).toBe(true);
  });

  it.each([
    ["spent -5 at Cinema #Entertainment", "amount", "-5", "invalid"],
    ["spent 5 gbp at Cinema #Entertainment", "currency", "gbp", "invalid"],
    ["spent 5 at Cinema #Entertainment date:2026-02-30", "date", "date:2026-02-30", "invalid"],
    ["moved 5 @Checking >Savings at Cinema", "merchant", "Cinema", "invalid"],
    ["moved 5 @Checking >Savings for Shopping", "category", "Shopping", "invalid"],
  ] as const)("returns the exact invalid range for %s", (input, field, text, status) => {
    const result = parse(input);
    const range = result.ranges.find(
      (candidate) => candidate.field === field && candidate.text === text
    );

    expect(range).toMatchObject({
      start: input.indexOf(text),
      end: input.indexOf(text) + text.length,
      text,
      status,
    });
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
    expect(result.announcement).toBe("tag: family recognized");
    expect(result.announcement).not.toContain(input);
  });
});
