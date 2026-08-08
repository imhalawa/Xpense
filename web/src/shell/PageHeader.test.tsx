import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { Button } from "@fluentui/react-components";
import PageHeader from "./PageHeader";

describe("PageHeader", () => {
  it("renders title, description and actions", () => {
    render(
      <PageHeader
        title="Budgets"
        description="Manage spending limits"
        actions={<Button>New budget</Button>}
      />
    );

    expect(screen.getByRole("heading", { name: "Budgets", level: 2 })).toBeDefined();
    expect(screen.getByText("Manage spending limits")).toBeDefined();
    expect(screen.getByRole("button", { name: "New budget" })).toBeDefined();
  });
});
