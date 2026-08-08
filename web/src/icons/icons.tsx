import Icon, { IconProps } from "./Icon";

export const OverviewIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M5 20v-6" />
    <path d="M12 20V4.5" />
    <path d="M19 20v-9.5" />
  </Icon>
);

export const TransactionsIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M4 8.75h13" />
    <path d="M13.75 5.5 17 8.75l-3.25 3.25" />
    <path d="M20 15.25H7" />
    <path d="M10.25 12 7 15.25l3.25 3.25" />
  </Icon>
);

export const BudgetsIcon = (props: IconProps) => (
  <Icon {...props}>
    <circle cx="12" cy="12" r="8" />
    <path d="M12 4v8h8" />
  </Icon>
);

export const ManageIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M4 8.5h7" />
    <path d="M15.5 8.5H20" />
    <circle cx="13.25" cy="8.5" r="2.25" />
    <path d="M4 15.5h3.5" />
    <path d="M12 15.5h8" />
    <circle cx="9.75" cy="15.5" r="2.25" />
  </Icon>
);

export const BellIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M6 9a6 6 0 0 1 12 0c0 4 1.2 5.5 2 6.5H4c.8-1 2-2.5 2-6.5Z" />
    <path d="M10 19a2 2 0 0 0 4 0" />
  </Icon>
);

export const ArrowUpIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M12 19V6" />
    <path d="M6.5 11.5 12 6l5.5 5.5" />
  </Icon>
);

export const ArrowDownIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M12 5v13" />
    <path d="M17.5 12.5 12 18l-5.5-5.5" />
  </Icon>
);

export const ChevronLeftIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M14.5 6 8.5 12l6 6" />
  </Icon>
);

export const ChevronRightIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M9.5 6l6 6-6 6" />
  </Icon>
);

export const AmountIcon = (props: IconProps) => (
  <Icon {...props}>
    <circle cx="12" cy="12" r="8" />
    <path d="M12 8.5v7" />
    <path d="M14.5 10.2c-.5-.9-1.5-1.4-2.6-1.4-1.5 0-2.6.8-2.6 2s1 1.7 2.6 2.1c1.6.4 2.6.9 2.6 2.1s-1.1 2-2.6 2c-1.1 0-2.1-.5-2.6-1.4" />
  </Icon>
);

export const DateIcon = (props: IconProps) => (
  <Icon {...props}>
    <rect x="3.5" y="5" width="17" height="15.5" rx="2.5" />
    <path d="M3.5 10h17" />
    <path d="M8 3.5V6.5" />
    <path d="M16 3.5V6.5" />
  </Icon>
);

export const CategoryIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M3.5 7.5a2 2 0 0 1 2-2h3.4a2 2 0 0 1 1.6.8l1 1.2h7a2 2 0 0 1 2 2v7.5a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2Z" />
  </Icon>
);

export const MerchantIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M4 9.5h16v9a1.5 1.5 0 0 1-1.5 1.5h-13A1.5 1.5 0 0 1 4 18.5Z" />
    <path d="M3.2 9.5 5 4.5h14l1.8 5" />
    <path d="M9.75 20v-4.5h4.5V20" />
  </Icon>
);

export const AccountIcon = (props: IconProps) => (
  <Icon {...props}>
    <rect x="2.5" y="5.5" width="19" height="13" rx="2.5" />
    <path d="M2.5 10h19" />
    <path d="M6 14.5h3.5" />
  </Icon>
);

export const TagsIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M3.5 11.2V5.5a2 2 0 0 1 2-2h5.7a2 2 0 0 1 1.4.6l7.3 7.3a2 2 0 0 1 0 2.8l-5.7 5.7a2 2 0 0 1-2.8 0L4.1 12.6a2 2 0 0 1-.6-1.4Z" />
    <circle cx="8" cy="8" r="1.4" />
  </Icon>
);

export const IncomeIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M7 17 17 7" />
    <path d="M9.5 7H17v7.5" />
  </Icon>
);

export const ExpenseIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M7 7l10 10" />
    <path d="M17 9.5V17H9.5" />
  </Icon>
);

export const EuroIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M18 6.8A6.5 6.5 0 0 0 8.2 12 6.5 6.5 0 0 0 18 17.2" />
    <path d="M4.5 10.3h8" />
    <path d="M4.5 13.7h8" />
  </Icon>
);

export const DollarIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M12 3.5v17" />
    <path d="M16 8.2c-.6-1.2-2-2-4-2-2.3 0-4 1.2-4 3s1.6 2.6 4 3.2c2.4.6 4 1.3 4 3.1s-1.7 3-4 3c-2 0-3.4-.8-4-2" />
  </Icon>
);

export const LightModeIcon = (props: IconProps) => (
  <Icon {...props}>
    <circle cx="12" cy="12" r="4" />
    <path d="M12 2.5v2.2" />
    <path d="M12 19.3v2.2" />
    <path d="M4.2 4.2 5.8 5.8" />
    <path d="M18.2 18.2l1.6 1.6" />
    <path d="M2.5 12h2.2" />
    <path d="M19.3 12h2.2" />
    <path d="M4.2 19.8 5.8 18.2" />
    <path d="M18.2 5.8l1.6-1.6" />
  </Icon>
);

export const DarkModeIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M20 14.2A8.2 8.2 0 0 1 9.8 4 8.5 8.5 0 1 0 20 14.2Z" />
  </Icon>
);

export const SystemModeIcon = (props: IconProps) => (
  <Icon {...props}>
    <rect x="2.5" y="4.5" width="19" height="12.5" rx="2" />
    <path d="M8.5 20.5h7" />
    <path d="M12 17v3.5" />
  </Icon>
);

export const CloseIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M6.5 6.5 17.5 17.5" />
    <path d="M17.5 6.5 6.5 17.5" />
  </Icon>
);

export const PlusIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M12 5.5v13" />
    <path d="M5.5 12h13" />
  </Icon>
);

export const AlertIcon = (props: IconProps) => (
  <Icon {...props}>
    <path d="M10.3 4.2 2.9 17.3A2 2 0 0 0 4.6 20.3h14.8a2 2 0 0 0 1.7-3L13.7 4.2a2 2 0 0 0-3.4 0Z" />
    <path d="M12 9v4.2" />
    <circle cx="12" cy="16.6" r="0.9" fill="currentColor" stroke="none" />
  </Icon>
);
