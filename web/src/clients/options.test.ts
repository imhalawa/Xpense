import { beforeEach, describe, expect, it, vi } from "vitest";
import axios from "axios";
import { listMerchants, listTags } from "./options";

vi.mock("axios");

const paramsOfLastCall = (): Record<string, string | number | undefined> =>
  vi.mocked(axios.get).mock.calls[0][1]!.params as Record<string, string | number | undefined>;

describe("listMerchants and listTags", () => {
  beforeEach(() => {
    vi.mocked(axios.get).mockReset();
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
});
