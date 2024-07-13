import type { Meta, StoryObj } from "@storybook/react";

import DataGrid, { IDataGridHeader } from "../components/DataGrid/DataGrid";
import { ActivityIcon, Settings } from "lucide-react";
import { ITag } from "../typings/ITag";
import { ReactNode } from "react";

const meta = {
  title: "Xpense Data Grid",
  component: DataGrid,
  // This component will have an automatically generated Autodocs entry: https://storybook.js.org/docs/writing-docs/autodocs
  tags: ["autodocs"],
  parameters: {
    // More on how to position stories at: https://storybook.js.org/docs/configure/story-layout
    layout: "fullscreen",
  },
  args: {},
} satisfies Meta<typeof DataGrid>;

export default meta;
type Story = StoryObj<typeof meta>;

interface model {
  field1: string;
  field2: string;
  field3: string;
  field5: string;
  field6: string;
  tags: ITag[];
}

const headers: IDataGridHeader<model>[] = [
  {
    headerName: "head2",
    order: 2,
    icon: <ActivityIcon />,
    field: "field2",
  },
  {
    headerName: "head1",
    order: 1,
    field: "field1",
  },
  {
    headerName: "head3",
    order: 3,
    icon: <Settings />,
    field: "field3",
  },
  {
    headerName: "head4",
    order: 6,
    field: "field6",
  },
  {
    headerName: "head5",
    order: 5,
    field: "field5",
  },
  {
    headerName: "tags",
    order: 7,
    field: "tags",
    render: (value: model): ReactNode => {
      return (
        <>
          {value.tags.map((tag) => (
            <div key={tag.id}>{tag.name}</div>
          ))}
        </>
      );
    },
  },
];

const rows: Array<model> = [
  {
    field1: "value1",
    field2: "value2",
    field3: "value3",
    field5: "value5",
    field6: "value6",
    tags: [],
  },
  {
    field1: "value1",
    field2: "value2",
    field3: "value3",
    field5: "value5",
    field6: "value6",
    tags: [],
  },
  {
    field1: "value1",
    field2: "value2",
    field3: "value3",
    field5: "value5",
    field6: "value6",
    tags: [],
  },
  {
    field1: "value1",
    field2: "value2",
    field3: "value3",
    field5: "value5",
    field6: "value6",
    tags: [
      { id: 1, name: "tag1" },
      { id: 2, name: "tag2" },
      { id: 3, name: "tag3" },
    ],
  },
];

export const Default: Story = {
  args: {
    headers: headers,
    rows: rows,
    dense: false,
  },
};

export const Dense: Story = {
  args: {
    headers: headers,
    rows: rows,
    dense: true,
  },
};
