import { Chip } from "@mui/material";
import { IPriority } from "../../../typings";

interface CategoryChipProps {
  id: number | null;
  name: string;
  priority: IPriority;
  onClick?: () => void;
  onDelete?: () => void;
}

const CategoryChip = ({ id, name, onClick, onDelete }: CategoryChipProps) => {
  return (
    <>
      <Chip key={id} label={name} onClick={onClick} onDelete={onDelete ?? undefined} />
    </>
  );
};

export default CategoryChip;
