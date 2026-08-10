import { Currency } from "../../typings/enums/Currency";
import {
  QuickAddAccount,
  QuickAddCategoryReference,
  QuickAddField,
  QuickAddIssue,
  QuickAddKind,
  QuickAddOption,
  QuickAddParseResult,
  QuickAddParserContext,
  QuickAddRange,
  QuickAddReference,
} from "./types";

interface Candidate<T> {
  value: T;
  rangeId: string;
}

interface DateCandidate {
  value: Date | null;
  rangeId: string;
}

interface KindMatch {
  value: QuickAddKind;
  start: number;
  end: number;
}

const currencyAliases: Record<string, Currency> = {
  "€": Currency.EUR,
  euro: Currency.EUR,
  euros: Currency.EUR,
  eur: Currency.EUR,
  "$": Currency.USD,
  dollar: Currency.USD,
  dollars: Currency.USD,
  usd: Currency.USD,
};

const isoCurrencyCodes = new Set(
  "AED AFN ALL AMD ANG AOA ARS AUD AWG AZN BAM BBD BDT BGN BHD BIF BMD BND BOB BOV BRL BSD BTN BWP BYN BZD CAD CDF CHE CHF CHW CLF CLP CNY COP COU CRC CUC CUP CVE CZK DJF DKK DOP DZD EGP ERN ETB EUR FJD FKP GBP GEL GHS GIP GMD GNF GTQ GYD HKD HNL HTG HUF IDR ILS INR IQD IRR ISK JMD JOD JPY KES KGS KHR KMF KPW KRW KWD KYD KZT LAK LBP LKR LRD LSL LYD MAD MDL MGA MKD MMK MNT MOP MRU MUR MVR MWK MXN MXV MYR MZN NAD NGN NIO NOK NPR NZD OMR PAB PEN PGK PHP PKR PLN PYG QAR RON RSD RUB RWF SAR SBD SCR SDG SEK SGD SHP SLE SLL SOS SRD SSP STN SVC SYP SZL THB TJS TMT TND TOP TRY TTD TWD TZS UAH UGX USD USN UYI UYU UYW UZS VED VES VND VUV WST XAF XAG XAU XBA XBB XBC XBD XCD XCG XDR XOF XPD XPF XPT XSU XTS XUA XXX YER ZAR ZMW ZWL".split(" ")
);

const kindWords = [
  { expression: /\bwas\s+paid\b/gi, kind: "income" as const },
  { expression: /\b(?:received|earned|deposited|refund)\b/gi, kind: "income" as const },
  { expression: /\b(?:moved|transferred|sent)\b/gi, kind: "transfer" as const },
  { expression: /\b(?:spent|paid|bought|purchased|charged|withdrew)\b/gi, kind: "expense" as const },
];

const connectiveWords = ["at", "in", "on", "from", "to", "for"];

const leadingConnectiveExpression = new RegExp(`^(?:${connectiveWords.join("|")})\\s+`, "i");

const trailingConnectiveExpression = new RegExp(`\\s+(?:${connectiveWords.join("|")})$`, "i");

const monthIndexes: Record<string, number> = {
  jan: 0,
  january: 0,
  feb: 1,
  february: 1,
  mar: 2,
  march: 2,
  apr: 3,
  april: 3,
  may: 4,
  jun: 5,
  june: 5,
  jul: 6,
  july: 6,
  aug: 7,
  august: 7,
  sep: 8,
  september: 8,
  oct: 9,
  october: 9,
  nov: 10,
  november: 10,
  dec: 11,
  december: 11,
};

const weekdayIndexes: Record<string, number> = {
  sun: 0,
  sunday: 0,
  mon: 1,
  monday: 1,
  tue: 2,
  tues: 2,
  tuesday: 2,
  wed: 3,
  wednesday: 3,
  thu: 4,
  thur: 4,
  thurs: 4,
  thursday: 4,
  fri: 5,
  friday: 5,
  sat: 6,
  saturday: 6,
};

const normalized = (value: string) => value.trim().toLocaleLowerCase();

const escapeExpression = (value: string) =>
  value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

const optionReference = (label: string, options: QuickAddOption[]): QuickAddReference => {
  const existing = options.find((option) => normalized(option.label) === normalized(label));
  return existing === undefined
    ? { id: "", label: label.trim(), create: true }
    : { ...existing, create: false };
};

const categoryReference = (
  label: string,
  categories: QuickAddOption[]
): QuickAddCategoryReference => {
  const existing = categories.find((category) => normalized(category.label) === normalized(label));
  return existing === undefined
    ? { id: "", label: label.trim(), create: true, priority: "Medium" }
    : { ...existing, create: false };
};

const parseCalendarDate = (value: string, now: Date, forcePreviousWeekday = false): Date | null => {
  const clean = normalized(value);
  const result = new Date(now);
  if (clean === "today") return result;
  if (clean === "yesterday") {
    result.setDate(result.getDate() - 1);
    return result;
  }

  const weekdayName = clean.replace(/^last\s+/, "");
  const weekday = weekdayIndexes[weekdayName];
  if (weekday !== undefined) {
    let daysBack = (result.getDay() - weekday + 7) % 7;
    if (forcePreviousWeekday || clean.startsWith("last ")) daysBack = daysBack === 0 ? 7 : daysBack;
    result.setDate(result.getDate() - daysBack);
    return result;
  }

  const iso = /^(\d{4})-(\d{2})-(\d{2})$/.exec(clean);
  if (iso !== null) {
    const year = Number(iso[1]);
    const month = Number(iso[2]) - 1;
    const day = Number(iso[3]);
    result.setFullYear(year, month, day);
    return result.getFullYear() === year && result.getMonth() === month && result.getDate() === day
      ? result
      : null;
  }

  const dayFirst = /^(\d{1,2})\s+([a-z]+)$/.exec(clean);
  const monthFirst = /^([a-z]+)\s+(\d{1,2})$/.exec(clean);
  const day = Number(dayFirst?.[1] ?? monthFirst?.[2]);
  const monthName = dayFirst?.[2] ?? monthFirst?.[1];
  const month = monthName === undefined ? undefined : monthIndexes[monthName];
  if (month === undefined || !Number.isInteger(day)) return null;
  result.setFullYear(now.getFullYear(), month, day);
  if (result.getMonth() !== month || result.getDate() !== day) return null;
  if (result.getTime() > now.getTime()) result.setFullYear(now.getFullYear() - 1);
  return result;
};

const parseClock = (value: string): { hours: number; minutes: number } | null => {
  const clean = normalized(value);
  if (clean === "this morning") return { hours: 9, minutes: 0 };
  if (clean === "this afternoon") return { hours: 15, minutes: 0 };
  if (clean === "this evening") return { hours: 19, minutes: 0 };
  const twelveHour = /^(1[0-2]|\d)(?::([0-5]\d))?\s*(am|pm)$/.exec(clean);
  if (twelveHour !== null) {
    const base = Number(twelveHour[1]) % 12;
    return {
      hours: base + (twelveHour[3] === "pm" ? 12 : 0),
      minutes: Number(twelveHour[2] ?? 0),
    };
  }
  const twentyFourHour = /^([01]?\d|2[0-3]):([0-5]\d)$/.exec(clean);
  return twentyFourHour === null
    ? null
    : { hours: Number(twentyFourHour[1]), minutes: Number(twentyFourHour[2]) };
};

const accountNames = (account: QuickAddAccount) => [account.label, account.id, ...(account.aliases ?? [])];

const resolveAccount = (
  label: string,
  accounts: QuickAddAccount[]
): { account: QuickAddAccount | null; candidates: QuickAddAccount[] } => {
  const wanted = normalized(label);
  const exact = accounts.filter((account) =>
    accountNames(account).some((name) => normalized(name) === wanted)
  );
  if (exact.length === 1) return { account: exact[0], candidates: exact };
  const prefix = accounts.filter((account) =>
    accountNames(account).some((name) => normalized(name).startsWith(wanted))
  );
  return { account: prefix.length === 1 ? prefix[0] : null, candidates: prefix };
};

export const parseQuickAdd = (
  input: string,
  context: QuickAddParserContext
): QuickAddParseResult => {
  const now = new Date(context.now ?? new Date());
  const occupied = Array.from({ length: input.length }, () => false);
  const ranges: QuickAddRange[] = [];
  const issues: QuickAddIssue[] = [];
  let rangeSequence = 0;

  const isFree = (start: number, end: number) =>
    occupied.slice(start, end).every((value) => !value);
  const consume = (start: number, end: number) => {
    for (let index = start; index < end; index += 1) occupied[index] = true;
  };
  const addRange = (
    start: number,
    end: number,
    field: QuickAddField,
    label: string,
    status: QuickAddRange["status"] = "recognized"
  ) => {
    const range: QuickAddRange = {
      id: `quick-add-range-${rangeSequence++}`,
      start,
      end,
      text: input.slice(start, end),
      field,
      label,
      status,
    };
    ranges.push(range);
    consume(start, end);
    return range.id;
  };
  const updateRanges = (rangeIds: string[], status: QuickAddRange["status"]) => {
    ranges.forEach((range) => {
      if (rangeIds.includes(range.id)) range.status = status;
    });
  };
  const addIssue = (
    code: string,
    field: QuickAddField,
    message: string,
    rangeIds: string[] = [],
    candidates?: string[]
  ) => {
    issues.push({ code, field, message, severity: "error", rangeIds, candidates });
  };

  const amountCandidates: Candidate<number>[] = [];
  const currencyCandidates: Candidate<Currency>[] = [];
  let currencyTokenCount = 0;
  const categoryCandidates: Candidate<QuickAddCategoryReference | null>[] = [];
  const merchantCandidates: Candidate<QuickAddReference>[] = [];
  const sourceCandidates: Candidate<QuickAddAccount | null>[] = [];
  const destinationCandidates: Candidate<QuickAddAccount | null>[] = [];
  const tagCandidates: Candidate<QuickAddReference>[] = [];
  const dateCandidates: DateCandidate[] = [];
  const timeCandidates: Candidate<{ hours: number; minutes: number } | null>[] = [];
  const reasonCandidates: Candidate<string>[] = [];

  const addMerchantCandidate = (rangeId: string, label: string) => {
    const merchant = optionReference(label, context.merchants);
    merchantCandidates.push({ value: merchant, rangeId });
    if (!merchant.create) return;
    updateRanges([rangeId], "unresolved");
    const range = ranges.find((candidate) => candidate.id === rangeId);
    if (range !== undefined) range.label = `No merchant matches ${merchant.label}`;
  };

  const explicitExpression = /\b(merchant|category|source|destination|tag|date|time|reason):(?:"([^"]+)"|([^\s]+))/gi;
  for (const match of input.matchAll(explicitExpression)) {
    const start = match.index;
    const end = start + match[0].length;
    const name = normalized(match[1]);
    const value = (match[2] ?? match[3]).trim();
    const field = name === "source" ? "sourceAccount" : name === "destination" ? "destinationAccount" : name;
    const rangeId = addRange(start, end, field as QuickAddField, `${field}: ${value}`);
    if (name === "merchant") addMerchantCandidate(rangeId, value);
    if (name === "category") {
      const category = categoryReference(value, context.categories);
      categoryCandidates.push({ value: category, rangeId });
      const range = ranges.find((candidate) => candidate.id === rangeId);
      if (category.create && range !== undefined) {
        range.label = `New category ${category.label}, priority ${category.priority}`;
      }
    }
    if (name === "source" || name === "destination") {
      const resolution = resolveAccount(value, context.accounts);
      const candidate = { value: resolution.account, rangeId };
      if (name === "source") sourceCandidates.push(candidate);
      else destinationCandidates.push(candidate);
      if (resolution.account === null) {
        updateRanges([rangeId], "unresolved");
        addIssue(
          resolution.candidates.length > 1 ? "ambiguous-account" : "unknown-account",
          field as QuickAddField,
          resolution.candidates.length > 1
            ? `${value} matches more than one account`
            : `No account matches ${value}`,
          [rangeId],
          resolution.candidates.map((account) => account.label)
        );
      }
    }
    if (name === "tag") tagCandidates.push({ value: optionReference(value, context.tags), rangeId });
    if (name === "date") {
      const parsedDate = parseCalendarDate(value, now);
      dateCandidates.push({ value: parsedDate, rangeId });
      if (parsedDate === null) {
        updateRanges([rangeId], "invalid");
        addIssue("invalid-date", "date", `${value} is not a valid date`, [rangeId]);
      }
    }
    if (name === "time") {
      const parsedTime = parseClock(value);
      timeCandidates.push({ value: parsedTime, rangeId });
      if (parsedTime === null) {
        updateRanges([rangeId], "invalid");
        addIssue("invalid-time", "time", `${value} is not a valid time`, [rangeId]);
      }
    }
    if (name === "reason") reasonCandidates.push({ value, rangeId });
  }

  const sigilExpression = /(^|\s)([#@>~])(?:"([^"]+)"|([^\s]+))/g;
  for (const match of input.matchAll(sigilExpression)) {
    const start = match.index + match[1].length;
    const end = match.index + match[0].length;
    if (!isFree(start, end)) continue;
    const sigil = match[2];
    const value = (match[3] ?? match[4]).replace(/[.,;!?]+$/, "");
    const field: QuickAddField =
      sigil === "#"
        ? "category"
        : sigil === "@"
          ? "sourceAccount"
          : sigil === ">"
            ? "destinationAccount"
            : "tag";
    const rangeId = addRange(start, end, field, `${field}: ${value}`);
    if (sigil === "#") {
      const category = categoryReference(value, context.categories);
      categoryCandidates.push({ value: category, rangeId });
      if (category.create) {
        const range = ranges.find((candidate) => candidate.id === rangeId);
        if (range !== undefined) range.label = `New category ${category.label}, priority ${category.priority}`;
      }
    } else if (sigil === "~") {
      tagCandidates.push({ value: optionReference(value, context.tags), rangeId });
    } else {
      const resolution = resolveAccount(value, context.accounts);
      const candidate = { value: resolution.account, rangeId };
      if (sigil === "@") sourceCandidates.push(candidate);
      else destinationCandidates.push(candidate);
      if (resolution.account === null) {
        updateRanges([rangeId], "unresolved");
        addIssue(
          resolution.candidates.length > 1 ? "ambiguous-account" : "unknown-account",
          field,
          resolution.candidates.length > 1
            ? `${value} matches more than one account`
            : `No account matches ${value}`,
          [rangeId],
          resolution.candidates.map((account) => account.label)
        );
      }
    }
  }

  const addDateMatch = (expression: RegExp, forcePreviousWeekday = false) => {
    for (const match of input.matchAll(expression)) {
      const start = match.index;
      const end = start + match[0].length;
      if (!isFree(start, end)) continue;
      const value = parseCalendarDate(match[0], now, forcePreviousWeekday);
      const rangeId = addRange(start, end, "date", value === null ? "Date not understood" : "Date");
      dateCandidates.push({ value, rangeId });
      if (value === null) {
        updateRanges([rangeId], "invalid");
        addIssue("invalid-date", "date", `${match[0]} is not a valid date`, [rangeId]);
      }
    }
  };
  addDateMatch(/\b\d{4}-\d{2}-\d{2}\b/g);
  addDateMatch(/\b(?:today|yesterday)\b/gi);
  addDateMatch(/\blast\s+(?:mon(?:day)?|tue(?:s|sday)?|wed(?:nesday)?|thu(?:r|rs|rsday)?|fri(?:day)?|sat(?:urday)?|sun(?:day)?)\b/gi, true);
  addDateMatch(/\b(?:mon(?:day)?|tue(?:s|sday)?|wed(?:nesday)?|thu(?:r|rs|rsday)?|fri(?:day)?|sat(?:urday)?|sun(?:day)?)\b/gi);
  addDateMatch(/\b(?:\d{1,2}\s+(?:jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)|(?:jan(?:uary)?|feb(?:ruary)?|mar(?:ch)?|apr(?:il)?|may|jun(?:e)?|jul(?:y)?|aug(?:ust)?|sep(?:tember)?|oct(?:ober)?|nov(?:ember)?|dec(?:ember)?)\s+\d{1,2})\b/gi);

  const addTimeMatch = (expression: RegExp) => {
    for (const match of input.matchAll(expression)) {
      const start = match.index;
      const end = start + match[0].length;
      if (!isFree(start, end)) continue;
      const value = parseClock(match[0]);
      const rangeId = addRange(start, end, "time", value === null ? "Time not understood" : "Time");
      timeCandidates.push({ value, rangeId });
      if (value === null) {
        updateRanges([rangeId], "invalid");
        addIssue("invalid-time", "time", `${match[0]} is not a valid time`, [rangeId]);
      }
    }
  };
  addTimeMatch(/\bthis\s+(?:morning|afternoon|evening)\b/gi);
  addTimeMatch(/\b(?:[01]?\d|2[0-3]):[0-5]\d\b/g);
  addTimeMatch(/\b(?:1[0-2]|\d)(?::[0-5]\d)?\s*(?:am|pm)\b/gi);

  const amountExpression = /-?\d+(?:[.,]\d+)?/g;
  for (const match of input.matchAll(amountExpression)) {
    const start = match.index;
    const end = start + match[0].length;
    if (!isFree(start, end)) continue;
    const decimal = match[0].replace(",", ".");
    const value = Number(decimal);
    const rangeId = addRange(start, end, "amount", `Amount ${decimal}`);
    amountCandidates.push({ value, rangeId });
    if (!Number.isFinite(value) || value <= 0) {
      updateRanges([rangeId], "invalid");
      addIssue("invalid-amount", "amount", "Amount must be greater than zero", [rangeId]);
    }
  }

  const currencyMatches = [
    ...input.matchAll(/€|\$|\b(?:euros?|dollars?|eur|usd)\b/gi),
    ...[...input.matchAll(/\b[a-z]{3}\b/gi)].filter((match) =>
      isoCurrencyCodes.has(match[0].toLocaleUpperCase())
    ),
  ].sort((left, right) => left.index - right.index);
  for (const match of currencyMatches) {
    const start = match.index;
    const end = start + match[0].length;
    if (!isFree(start, end)) continue;
    currencyTokenCount += 1;
    const currency = currencyAliases[normalized(match[0])];
    const rangeId = addRange(start, end, "currency", currency === undefined ? "Unsupported currency" : `Currency ${currency}`);
    if (currency === undefined || !(context.supportedCurrencies ?? Object.values(Currency)).includes(currency)) {
      updateRanges([rangeId], "invalid");
      addIssue("unsupported-currency", "currency", `${match[0]} is not a supported currency`, [rangeId]);
    } else {
      currencyCandidates.push({ value: currency, rangeId });
    }
  }

  const kindMatches: KindMatch[] = [];
  for (const definition of kindWords) {
    for (const match of input.matchAll(definition.expression)) {
      const start = match.index;
      const end = start + match[0].length;
      if (!isFree(start, end)) continue;
      consume(start, end);
      kindMatches.push({ value: definition.kind, start, end });
    }
  }
  const highlightKindMatches = (status: QuickAddRange["status"]) =>
    kindMatches.map((match) => addRange(match.start, match.end, "kind", match.value, status));

  const conflict = <T>(field: QuickAddField, candidates: Candidate<T>[], message: string) => {
    if (candidates.length < 2) return false;
    const rangeIds = candidates.map((candidate) => candidate.rangeId);
    updateRanges(rangeIds, "conflict");
    addIssue(`conflicting-${field}`, field, message, rangeIds);
    return true;
  };

  const kindConflict = kindMatches.length > 1;
  if (kindConflict) {
    addIssue(
      "conflicting-kind",
      "kind",
      "More than one transaction kind was provided",
      highlightKindMatches("conflict")
    );
  }
  let kind = kindConflict ? null : (kindMatches[0]?.value ?? null);

  const parseNaturalAccounts = (side: "source" | "destination", preposition: "from" | "to") => {
    const candidates = side === "source" ? sourceCandidates : destinationCandidates;
    if (candidates.length > 0) return;
    const sortedAccounts = [...context.accounts].sort((left, right) => right.label.length - left.label.length);
    for (const account of sortedAccounts) {
      const expression = new RegExp(`\\b${preposition}\\s+(?:"${escapeExpression(account.label)}"|${escapeExpression(account.label)})\\b`, "i");
      const match = expression.exec(input);
      if (match === null) continue;
      const labelOffset = match[0].toLocaleLowerCase().lastIndexOf(account.label.toLocaleLowerCase());
      const start = match.index + labelOffset;
      const end = start + account.label.length;
      if (!isFree(start, end)) continue;
      consume(match.index, start);
      const rangeId = addRange(start, end, side === "source" ? "sourceAccount" : "destinationAccount", `${side} ${account.label}`);
      candidates.push({ value: account, rangeId });
      break;
    }
  };

  if (kind === "transfer") {
    parseNaturalAccounts("source", "from");
    parseNaturalAccounts("destination", "to");
  } else if (kind === "income") {
    parseNaturalAccounts("destination", "to");
  } else {
    parseNaturalAccounts("source", "from");
  }

  if (kind === null && !kindConflict) {
    if (sourceCandidates.length > 0 && destinationCandidates.length > 0) kind = "transfer";
    else if (destinationCandidates.length > 0) kind = "income";
    else kind = "expense";
  }

  const accountKind =
    sourceCandidates.length > 0 && destinationCandidates.length > 0
      ? "transfer"
      : destinationCandidates.length > 0
        ? "income"
        : sourceCandidates.length > 0
          ? "expense"
          : null;
  if (kind !== null && accountKind !== null && kind !== accountKind) {
    const rangeIds = [
      ...highlightKindMatches("conflict"),
      ...sourceCandidates.map((candidate) => candidate.rangeId),
      ...destinationCandidates.map((candidate) => candidate.rangeId),
    ];
    updateRanges(rangeIds, "conflict");
    addIssue("kind-account-conflict", "kind", "The transaction kind conflicts with its account direction", rangeIds);
    kind = null;
  }

  const parseNaturalCategory = () => {
    if (categoryCandidates.length > 0 || kind === "transfer") return;
    const sorted = [...context.categories].sort((left, right) => right.label.length - left.label.length);
    for (const category of sorted) {
      const expression = new RegExp(`\\bfor\\s+(?:"${escapeExpression(category.label)}"|${escapeExpression(category.label)})\\b`, "i");
      const match = expression.exec(input);
      if (match === null) continue;
      const labelOffset = match[0].toLocaleLowerCase().lastIndexOf(category.label.toLocaleLowerCase());
      const start = match.index + labelOffset;
      const end = start + category.label.length;
      if (!isFree(start, end)) continue;
      consume(match.index, start);
      const rangeId = addRange(start, end, "category", `Category ${category.label}`);
      categoryCandidates.push({ value: { ...category, create: false }, rangeId });
      break;
    }
  };
  parseNaturalCategory();

  if (kind !== "transfer" && categoryCandidates.length === 0) {
    const unknownCategoryExpression = /\bfor\s+("[^"]+"|[^\s]+)/gi;
    for (const match of input.matchAll(unknownCategoryExpression)) {
      const valueWithQuotes = match[1];
      const value = valueWithQuotes.replace(/^"|"$/g, "");
      const valueOffset = match[0].lastIndexOf(valueWithQuotes);
      const start = match.index + valueOffset;
      const end = start + valueWithQuotes.length;
      if (!isFree(start, end)) continue;
      consume(match.index, start);
      const rangeId = addRange(start, end, "category", `Category ${value}`, "unresolved");
      categoryCandidates.push({ value: null, rangeId });
      addIssue("unknown-category", "category", `No category matches ${value}`, [rangeId]);
      break;
    }
  }

  const remainingParts = (): Array<{ start: number; end: number; text: string }> => {
    const parts: Array<{ start: number; end: number; text: string }> = [];
    let start: number | null = null;
    for (let index = 0; index <= input.length; index += 1) {
      const available = index < input.length && !occupied[index];
      if (available && start === null) start = index;
      if ((!available || index === input.length) && start !== null) {
        const raw = input.slice(start, index);
        const leading = raw.search(/\S/);
        const trailing = raw.search(/\s*$/);
        if (leading >= 0 && trailing > leading) {
          parts.push({ start: start + leading, end: start + trailing, text: raw.slice(leading, trailing) });
        }
        start = null;
      }
    }
    return parts;
  };

  const rejectNaturalTransferField = (
    preposition: "at" | "for",
    field: "merchant" | "category",
    code: string,
    message: string
  ) => {
    const expression = preposition === "at"
      ? /\bat\s+(.+?)(?=\s+for\b|$)/i
      : /\bfor\s+(.+?)(?=\s+at\b|$)/i;
    for (const part of remainingParts()) {
      const match = expression.exec(part.text);
      if (match === null) continue;
      const phraseOffset = match[0].indexOf(match[1]);
      const start = part.start + match.index + phraseOffset;
      const end = start + match[1].length;
      if (!isFree(start, end)) continue;
      consume(part.start + match.index, start);
      const rangeId = addRange(start, end, field, `${field === "merchant" ? "Merchant" : "Category"} ${match[1]}`, "invalid");
      addIssue(code, field, message, [rangeId]);
    }
  };

  if (kind === "transfer") {
    rejectNaturalTransferField("at", "merchant", "transfer-merchant", "A transfer cannot have a merchant");
    rejectNaturalTransferField("for", "category", "transfer-category", "A transfer cannot have a category");
  }

  const takeRemainder = (field: "merchant" | "reason") => {
    const parts = remainingParts();
    if (parts.length === 0) return;
    const first = parts[0];
    const last = parts[parts.length - 1];
    let start = first.start;
    let end = last.end;
    let text = input
      .slice(start, end)
      .trim()
      .replace(leadingConnectiveExpression, "")
      .replace(trailingConnectiveExpression, "")
      .trim();
    const offset = input.slice(start, end).indexOf(text);
    if (offset >= 0) start += offset;
    end = start + text.length;
    if (text.length === 0) {
      consume(first.start, last.end);
      return;
    }
    consume(first.start, start);
    consume(end, last.end);
    const rangeId = addRange(start, end, field, `${field === "merchant" ? "Merchant" : "Reason"} ${text}`);
    if (field === "merchant") addMerchantCandidate(rangeId, text);
    else reasonCandidates.push({ value: text, rangeId });
  };

  if (kind === "transfer" && reasonCandidates.length === 0) takeRemainder("reason");
  if (kind !== "transfer" && merchantCandidates.length === 0) takeRemainder("merchant");

  const unresolved = remainingParts();
  for (const part of unresolved) {
    const rangeId = addRange(part.start, part.end, "text", "Not understood", "unresolved");
    addIssue("unresolved-text", "text", `${part.text} was not understood`, [rangeId]);
  }

  const amountConflict = conflict("amount", amountCandidates, "More than one amount was provided");
  const currencyConflict = conflict("currency", currencyCandidates, "More than one currency was provided");
  const categoryConflict = conflict("category", categoryCandidates, "More than one category was provided");
  const merchantConflict = conflict("merchant", merchantCandidates, "More than one merchant was provided");
  const sourceConflict = conflict("sourceAccount", sourceCandidates, "More than one source account was provided");
  const destinationConflict = conflict("destinationAccount", destinationCandidates, "More than one destination account was provided");
  const dateConflict = dateCandidates.length > 1;
  if (dateConflict) {
    const rangeIds = dateCandidates.map((candidate) => candidate.rangeId);
    updateRanges(rangeIds, "conflict");
    addIssue("conflicting-date", "date", "More than one date was provided", rangeIds);
  }
  const timeConflict = conflict("time", timeCandidates, "More than one time was provided");
  const reasonConflict = conflict("reason", reasonCandidates, "More than one transfer reason was provided");

  const defaultAccount = context.accounts.find((account) => account.id === context.defaultAccountId) ?? null;
  let sourceAccount = sourceConflict ? null : (sourceCandidates[0]?.value ?? null);
  let destinationAccount = destinationConflict ? null : (destinationCandidates[0]?.value ?? null);
  if (kind === "expense" && sourceCandidates.length === 0) sourceAccount = defaultAccount;
  if (kind === "income" && destinationCandidates.length === 0) destinationAccount = defaultAccount;

  let currency = currencyConflict ? null : (currencyCandidates[0]?.value ?? null);
  const chosenAccount = sourceAccount ?? destinationAccount;
  if (currency === null && currencyTokenCount === 0) currency = chosenAccount?.currency ?? null;

  if (sourceAccount !== null && destinationAccount !== null) {
    if (sourceAccount.id === destinationAccount.id) {
      addIssue("same-transfer-account", "destinationAccount", "A transfer needs two different accounts");
    }
    if (sourceAccount.currency !== destinationAccount.currency) {
      addIssue("cross-currency-transfer", "currency", "Transfers between different currencies are not supported");
    }
  }
  if (currency !== null && chosenAccount !== null && currency !== chosenAccount.currency) {
    addIssue("account-currency-mismatch", "currency", `The amount must use ${chosenAccount.currency} for ${chosenAccount.label}`);
  }

  const selectedDate = dateConflict ? null : (dateCandidates[0]?.value ?? new Date(now));
  const selectedTime = timeConflict ? null : (timeCandidates[0]?.value ?? null);
  if (selectedDate !== null && selectedTime !== null) {
    selectedDate.setHours(selectedTime.hours, selectedTime.minutes, 0, 0);
  }
  const selectedCalendarDay = selectedDate === null
    ? null
    : new Date(selectedDate.getFullYear(), selectedDate.getMonth(), selectedDate.getDate());
  const currentCalendarDay = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  if (selectedCalendarDay !== null && selectedCalendarDay.getTime() > currentCalendarDay.getTime()) {
    const rangeIds = dateCandidates.map((candidate) => candidate.rangeId);
    updateRanges(rangeIds, "invalid");
    addIssue("future-date", "date", "Future transactions are not supported", rangeIds);
  }

  const category = categoryConflict ? null : (categoryCandidates[0]?.value ?? null);
  const merchant = merchantConflict ? null : (merchantCandidates[0]?.value ?? null);
  const amount = amountConflict ? null : (amountCandidates[0]?.value ?? null);
  const amountMinorUnits = amount === null || amount <= 0 ? null : Math.round(amount * 100);
  const reason = reasonConflict ? null : (reasonCandidates[0]?.value ?? null);

  if (amountCandidates.length === 0) addIssue("missing-amount", "amount", "Enter an amount");
  if (currency === null && currencyTokenCount === 0) addIssue("missing-currency", "currency", "Choose a currency or account");
  if (kind === "expense" && sourceAccount === null && sourceCandidates.length === 0) addIssue("missing-source-account", "sourceAccount", "Choose a source account");
  if (kind === "income" && destinationAccount === null && destinationCandidates.length === 0) addIssue("missing-destination-account", "destinationAccount", "Choose a destination account");
  if (kind === "transfer" && sourceAccount === null && sourceCandidates.length === 0) addIssue("missing-source-account", "sourceAccount", "Choose a source account");
  if (kind === "transfer" && destinationAccount === null && destinationCandidates.length === 0) addIssue("missing-destination-account", "destinationAccount", "Choose a destination account");
  if (kind !== "transfer" && merchant === null && merchantCandidates.length === 0) addIssue("missing-merchant", "merchant", "Enter a merchant or payer");
  if (kind !== "transfer" && category === null && categoryCandidates.length === 0) addIssue("missing-category", "category", "Choose a category");
  if (kind === "transfer" && merchantCandidates.length > 0) {
    const rangeIds = merchantCandidates.map((candidate) => candidate.rangeId);
    updateRanges(rangeIds, "invalid");
    addIssue("transfer-merchant", "merchant", "A transfer cannot have a merchant", rangeIds);
  }
  if (kind === "transfer" && categoryCandidates.length > 0) {
    const rangeIds = categoryCandidates.map((candidate) => candidate.rangeId);
    updateRanges(rangeIds, "invalid");
    addIssue("transfer-category", "category", "A transfer cannot have a category", rangeIds);
  }

  if (kind !== "transfer" && reasonCandidates.length > 0) {
    const rangeIds = reasonCandidates.map((candidate) => candidate.rangeId);
    updateRanges(rangeIds, "invalid");
    addIssue("non-transfer-reason", "reason", "Only transfers can have a reason", rangeIds);
  }

  const orderedRanges = [...ranges].sort((left, right) => left.start - right.start);
  const lastRange = orderedRanges[orderedRanges.length - 1];
  const announcement = input.trim().length === 0
    ? "Quick Add is empty"
    : lastRange === undefined
      ? (issues[0]?.message ?? "No fields recognised")
      : `${lastRange.label} ${lastRange.status}`;

  return {
    input,
    draft: {
      kind,
      amountMinorUnits,
      currency,
      occurredAt: selectedDate?.toISOString() ?? null,
      sourceAccount,
      destinationAccount,
      merchant,
      category,
      tags: tagCandidates.map((candidate) => candidate.value),
      reason,
    },
    ranges: orderedRanges,
    issues,
    canSubmit: issues.every((issue) => issue.severity !== "error"),
    announcement,
  };
};
