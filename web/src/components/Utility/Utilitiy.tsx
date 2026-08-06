import { Box } from "@mui/material";
import { ReactNode } from "react";

interface IUtilityProps {
  children: ReactNode | ReactNode[];
}

const Utility = ({ children }: IUtilityProps) => {
  return (
    <Box
      sx={{
        display: "flex",
        flexDirection: "column",
        padding: "1rem",
        height: "calc(100% - 4rem)",
        justifyContent: "space-between",
        overflowY: "scroll"
      }}>
      <Box
        sx={{
          width: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "center"
        }}>
        {children}
      </Box>
    </Box>
  );
};

export default Utility;
