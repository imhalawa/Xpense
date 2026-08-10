import { beforeEach, describe, expect, it, vi } from "vitest";
import axios from "axios";
import { createCategory, listMerchants, listPriorities, listTags } from "./options";

vi.mock("axios");

const paramsOfLastCall = (): Record<string, string | number | undefined> =>
  vi.mocked(axios.get).mock.calls[0][1]!.params as Record<string, string | number | undefined>;

describe("listMerchants and listTags", () => {
  beforeEach(() => {
    vi.mocked(axios.get).mockReset();
    vi.mocked(axios.post).mockReset();
    vi.mocked(axios.get).mockResolvedValue({ data: [] });
  });

  it("asks for merchants without a search when nothing was typed", async () => {
    await listMerchants();
    expect(paramsOfLastCall().search).toBeUndefined();
  });

  it("asks for merchants without a search when only spaces were typed", async () => {
    await listMerchants({ search: "   " });
    expect(paramsOfLastCall().search).toBeUndefined();
  });

  it("sends the typed text and the limit for merchants", async () => {
    await listMerchants({ search: " albert ", limit: 20 });
    expect(paramsOfLastCall().search).toBe("albert");
    expect(paramsOfLastCall().limit).toBe(20);
  });

  it("sends the typed text for tags", async () => {
    await listTags({ search: "travel" });
    expect(paramsOfLastCall().search).toBe("travel");
  });

  it("loads category priorities for pending Quick Add categories", async () => {
    await listPriorities();

    expect(vi.mocked(axios.get)).toHaveBeenCalledWith("/api/v1/priorities");
  });

  it("creates a category with its selected priority", async () => {
    const category = { id: 8, label: "New food" };
    vi.mocked(axios.post).mockResolvedValue({ data: category });

    await expect(createCategory({ label: "New food", priorityId: 3 })).resolves.toBe(category);
    expect(vi.mocked(axios.post)).toHaveBeenCalledWith("/api/v1/categories", {
      label: "New food",
      priorityId: 3,
    });
  });
});
