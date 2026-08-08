import { beforeEach, describe, expect, it, vi } from "vitest";
import axios from "axios";
import dayjs from "dayjs";
import { listTransactions } from "./transactions";

vi.mock("axios");

const emptyPage = { data: { items: [], page: 1, pageSize: 10, totalPages: 0, totalItems: 0 } };

const paramsOfLastCall = (): Record<string, string | undefined> =>
  vi.mocked(axios.get).mock.calls[0][1]!.params as Record<string, string | undefined>;

describe("listTransactions", () => {
  beforeEach(() => {
    vi.mocked(axios.get).mockReset();
    vi.mocked(axios.get).mockResolvedValue(emptyPage);
  });

  it("sends no range when neither a day nor a range is asked for", async () => {
    await listTransactions({ page: 1, pageSize: 10 });
    expect(paramsOfLastCall().from).toBeUndefined();
    expect(paramsOfLastCall().to).toBeUndefined();
  });

  it("turns a single day into the half open range the api expects", async () => {
    await listTransactions({ page: 1, pageSize: 10, day: dayjs("2026-08-07T13:45:00") });
    expect(paramsOfLastCall().from).toBe(dayjs("2026-08-07T00:00:00").toISOString());
    expect(paramsOfLastCall().to).toBe(dayjs("2026-08-08T00:00:00").toISOString());
  });

  it("keeps the last chosen day inside the range by moving the end one day on", async () => {
    await listTransactions({
      page: 1,
      pageSize: 10,
      from: dayjs("2026-08-01"),
      to: dayjs("2026-08-31"),
    });
    expect(paramsOfLastCall().from).toBe(dayjs("2026-08-01T00:00:00").toISOString());
    expect(paramsOfLastCall().to).toBe(dayjs("2026-09-01T00:00:00").toISOString());
  });

  it("prefers an explicit range over a day", async () => {
    await listTransactions({
      page: 1,
      pageSize: 10,
      day: dayjs("2026-01-01"),
      from: dayjs("2026-08-01"),
      to: dayjs("2026-08-31"),
    });
    expect(paramsOfLastCall().from).toBe(dayjs("2026-08-01T00:00:00").toISOString());
  });
});
