import { ReactNode } from "react";
import Box from "@mui/material/Box";
import Grid from "@mui/material/Grid";
import Typography from "@mui/material/Typography";

interface IPageProps {
  title: string;
  actions?: ReactNode;
  children: ReactNode[] | ReactNode;
}

const Page = ({ title, actions, children }: IPageProps) => {
  return (
    <Box sx={{ width: "100%" }}>
      <Box
        sx={{
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 2,
          marginBottom: { xs: 2, md: 3 },
        }}>
        <Typography variant="h1">{title}</Typography>
        {actions}
      </Box>
      <Grid container spacing={3}>
        {children}
      </Grid>
    </Box>
  );
};

export default Page;
