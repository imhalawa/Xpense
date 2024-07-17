import { Box } from "@mui/material";
import { ReactNode } from "react";

interface IUtilityProps {
  children: ReactNode | ReactNode[];
}

const Utility = ({ children }: IUtilityProps) => {
  return (
    <Box
      display="flex"
      flexDirection={"column"}
      padding={"1rem"}
      height="calc(100% - 4rem)"
      justifyContent={"space-between"}
      sx={{ overflowY: "scroll" }}
    >
      <Box width={"100%"} display="flex" flexDirection={"column"} justifyContent={"center"}>
        {children}
      </Box>
    </Box>
  );
};

export default Utility;
