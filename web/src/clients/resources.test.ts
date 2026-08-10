import { beforeEach, describe, expect, it, vi } from "vitest";
import axios from "axios";
import { createAccount, deleteAccount, updateAccount } from "./accounts";
import { updateCategory } from "./categories";
import { createMerchant } from "./merchants";
import { createTag } from "./tags";
import { Currency } from "../typings/enums/Currency";

vi.mock("axios");

describe("resource clients", () => {
  beforeEach(() => {
    vi.mocked(axios.post).mockReset();
    vi.mocked(axios.put).mockReset();
    vi.mocked(axios.delete).mockReset();
    vi.mocked(axios.post).mockResolvedValue({ data: { id: 1 } });
    vi.mocked(axios.put).mockResolvedValue({ data: { id: 1 } });
    vi.mocked(axios.delete).mockResolvedValue({ data: undefined });
  });

  it("uses the real CRUD routes and preserves colour payloads", async () => {
    await createAccount({ label: "Cash", balance: { minorUnits: 0, currency: Currency.EUR } });
    await updateAccount("NL 1", { label: "Cash", isDefault: true });
    await deleteAccount("NL 1");
    await updateCategory("7", { label: "Food", priorityId: 3 });
    await createTag({ label: "Travel", bgColorHex: "#EDEDED", fgColorHex: "#242424" });
    await createMerchant({ label: "Bakery" });

    expect(vi.mocked(axios.post)).toHaveBeenCalledWith("/api/v1/accounts", { label: "Cash", balance: { minorUnits: 0, currency: Currency.EUR } });
    expect(vi.mocked(axios.put)).toHaveBeenCalledWith("/api/v1/accounts/NL%201", { label: "Cash", isDefault: true });
    expect(vi.mocked(axios.delete)).toHaveBeenCalledWith("/api/v1/accounts/NL%201");
    expect(vi.mocked(axios.put)).toHaveBeenCalledWith("/api/v1/categories/7", { label: "Food", priorityId: 3 });
    expect(vi.mocked(axios.post)).toHaveBeenCalledWith("/api/v1/tags", { label: "Travel", bgColorHex: "#EDEDED", fgColorHex: "#242424" });
    expect(vi.mocked(axios.post)).toHaveBeenCalledWith("/api/v1/merchants", { label: "Bakery" });
  });
});
