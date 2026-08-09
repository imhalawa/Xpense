import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { Currency } from "../typings/enums/Currency";
import { loadLegacyClaimPasskeys, unlockLegacyClaim } from "../claim/claimUnlock";
import { fixtureProjection } from "./fixtureProjection";
import { VaultProvider } from "./VaultProvider";
import { VaultUnlockGate } from "./VaultUnlockGate";

vi.mock("../claim/claimUnlock", async (loadOriginal) => {
  const original = await loadOriginal<typeof import("../claim/claimUnlock")>();
  return { ...original, loadLegacyClaimPasskeys: vi.fn(), unlockLegacyClaim: vi.fn() };
});

beforeEach(() => {
  vi.mocked(loadLegacyClaimPasskeys).mockResolvedValue([
    { id: "11111111-1111-4111-8111-111111111111", label: "Laptop" },
    { id: "22222222-2222-4222-8222-222222222222", label: "Phone" },
  ]);
  vi.mocked(unlockLegacyClaim).mockResolvedValue(false);
});

describe("normal vault unlock gate", () => {
  it("requires an explicit wrapper selection when multiple passkeys exist", async () => {
    const projection = fixtureProjection({
      spaces: [{ id: "personal", name: "Personal", kind: "personal", canEdit: true }],
      accounts: { personal: [{ id: crypto.randomUUID(), label: "Cash", currency: Currency.EUR, canEdit: true }] },
      taxonomy: { personal: [] },
      transactions: { personal: [] },
    });
    Object.defineProperty(projection, "dataMode", { value: "encrypted" });
    projection.lock();
    render(
      <MemoryRouter>
        <VaultProvider projection={projection} autoUnlock={false}>
          <VaultUnlockGate><div>Private app</div></VaultUnlockGate>
        </VaultProvider>
      </MemoryRouter>,
    );

    const select = await screen.findByLabelText("Passkey");
    expect((select as HTMLSelectElement).value).toBe("");
    fireEvent.change(select, { target: { value: "22222222-2222-4222-8222-222222222222" } });
    fireEvent.click(screen.getByRole("button", { name: "Unlock" }));

    await waitFor(() => expect(unlockLegacyClaim).toHaveBeenCalledWith(
      expect.any(Function),
      "22222222-2222-4222-8222-222222222222",
    ));
    expect(screen.queryByText("Private app")).toBeNull();
  });
});
