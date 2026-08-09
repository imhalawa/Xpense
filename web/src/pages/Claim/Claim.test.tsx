import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { ClaimRecordCipher, ClaimStatus } from "../../claim/claimFlow";
import type { ClaimPasskeySelection } from "../../claim/claimUnlock";
import { ClaimView, type ClaimViewDependencies } from "./Claim";

const cipher: ClaimRecordCipher = {
  isReady: () => true,
  encrypt: vi.fn(),
};

const dependencies = (
  run: ClaimViewDependencies["run"],
): ClaimViewDependencies => ({
  api: {} as ClaimViewDependencies["api"],
  cipher,
  openDatabase: vi.fn().mockResolvedValue({ close: vi.fn() }),
  unlock: vi.fn().mockResolvedValue(true),
  run,
});

describe("Claim", () => {
  it("does not mistake an unlocked plaintext projection for Worker encryption readiness", () => {
    render(
      <ClaimView encryptionReady={false} dependencies={dependencies(vi.fn())} />,
    );

    expect(screen.getByRole("heading", { name: "Claim legacy data" })).toBeVisible();
    expect(screen.getByText("Unlock your vault before starting the claim.")).toBeVisible();
    expect(screen.getByRole("button", { name: "Start claim" })).toBeDisabled();
  });

  it("offers a real passkey unlock action on the standalone route", async () => {
    const claimDependencies = dependencies(vi.fn());
    render(
      <ClaimView
        encryptionReady={false}
        dependencies={claimDependencies}
        passkeys={[{ id: "wrapper-1", label: "Laptop" }]}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Unlock with passkey" }));

    await waitFor(() => expect(claimDependencies.unlock).toHaveBeenCalledWith("wrapper-1"));
  });

  it("requires an explicit passkey choice when multiple wrappers are available", async () => {
    const claimDependencies = dependencies(vi.fn());
    const passkeys: ClaimPasskeySelection[] = [
      { id: "wrapper-1", label: "Laptop" },
      { id: "wrapper-2", label: "Phone" },
    ];
    render(
      <ClaimView encryptionReady={false} dependencies={claimDependencies} passkeys={passkeys} />,
    );
    const unlockButton = screen.getByRole("button", { name: "Unlock with passkey" });

    expect(unlockButton).toBeDisabled();
    fireEvent.change(screen.getByLabelText("Passkey"), { target: { value: "wrapper-2" } });
    fireEvent.click(unlockButton);

    await waitFor(() => expect(claimDependencies.unlock).toHaveBeenCalledWith("wrapper-2"));
  });

  it("shows exact non-sensitive progress and a verified completion", async () => {
    const run = vi.fn(async (options) => {
      const statuses: ClaimStatus[] = [
        { phase: "downloading", completedRecords: 0, totalRecords: 0 },
        { phase: "encrypting", completedRecords: 1, totalRecords: 2 },
        { phase: "verifying", completedRecords: 2, totalRecords: 2 },
      ];
      for (const status of statuses) options.onStatus?.(status);
      return {
        recordCount: 2,
        counts: { account: 1, transaction: 1 },
        manifestHash: "a".repeat(64),
        completedAt: "2026-08-09T10:00:00Z",
      };
    });
    render(<ClaimView encryptionReady dependencies={dependencies(run)} />);

    fireEvent.click(screen.getByRole("button", { name: "Start claim" }));

    await screen.findByText("Legacy data encrypted and verified.");
    expect(screen.getByText("2 records verified")).toBeVisible();
    expect(document.body.textContent).not.toContain("a".repeat(64));
    expect(window.localStorage.length).toBe(0);
    expect(window.sessionStorage.length).toBe(0);
  });

  it("aborts and clears the active flow when the vault locks", async () => {
    let observedSignal: AbortSignal | undefined;
    const run = vi.fn(async (options) => {
      observedSignal = options.signal;
      await new Promise<void>((_resolve, reject) => {
        options.signal?.addEventListener("abort", () => reject(
          new DOMException("cancelled", "AbortError"),
        ));
      });
      throw new Error("unreachable");
    });
    const view = render(
      <ClaimView encryptionReady dependencies={dependencies(run)} />,
    );
    fireEvent.click(screen.getByRole("button", { name: "Start claim" }));
    await waitFor(() => expect(observedSignal).toBeDefined());

    view.rerender(<ClaimView encryptionReady={false} dependencies={dependencies(run)} />);

    await waitFor(() => expect(observedSignal?.aborted).toBe(true));
    expect(screen.getByText("Unlock your vault before starting the claim.")).toBeVisible();
  });

  it("returns to a restartable idle state after explicit cancellation", async () => {
    const run = vi.fn(async (options) => {
      await new Promise<void>((_resolve, reject) => {
        options.signal?.addEventListener("abort", () => reject(
          new DOMException("cancelled", "AbortError"),
        ));
      });
      throw new Error("unreachable");
    });
    render(<ClaimView encryptionReady dependencies={dependencies(run)} />);
    fireEvent.click(screen.getByRole("button", { name: "Start claim" }));
    const cancel = await screen.findByRole("button", { name: "Cancel claim" });

    fireEvent.click(cancel);

    await screen.findByRole("button", { name: "Start claim" });
    expect(screen.queryByRole("button", { name: "Cancel claim" })).not.toBeInTheDocument();
  });

  it("keeps an interrupted claim restartable", async () => {
    const run = vi.fn().mockRejectedValueOnce(new Error("The connection was interrupted."));
    render(<ClaimView encryptionReady dependencies={dependencies(run)} />);

    fireEvent.click(screen.getByRole("button", { name: "Start claim" }));

    await screen.findByText("The connection was interrupted.");
    expect(screen.getByText("Encrypted progress is safe. Start again to resume.")).toBeVisible();
    expect(screen.getByRole("button", { name: "Resume claim" })).toBeEnabled();
  });
});
