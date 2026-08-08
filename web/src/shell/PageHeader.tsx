import { ReactNode } from "react";
import { Caption1, Title2 } from "@fluentui/react-components";

interface PageHeaderProps {
  title: string;
  description?: string;
  actions?: ReactNode;
}

const PageHeader = ({ title, description, actions }: PageHeaderProps) => {
  return (
    <header
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 16,
        marginBottom: 24,
      }}>
      <div>
        <Title2>{title}</Title2>
        {description && <Caption1>{description}</Caption1>}
      </div>
      {actions && <div>{actions}</div>}
    </header>
  );
};

export default PageHeader;
