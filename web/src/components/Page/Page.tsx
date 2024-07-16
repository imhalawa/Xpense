import { Grid, Typography } from "@mui/material";
import { ReactNode } from "react";

interface IPageProps {
  title: string;
  headerBackgroundColor?: string;
  headerColor?: string;
  children: ReactNode[];
}
const Page = ({ title, children, headerBackgroundColor, headerColor }: IPageProps) => {
  return (
    <>
      <Grid
        item
        xs={12}
        sx={{
          backgroundColor: headerBackgroundColor,
          color: headerColor,
          borderTopLeftRadius: "1rem",
          borderTopRightRadius: "1rem",
        }}
      >
        <Typography variant="h4" m={2}>
          {title}
        </Typography>
        <hr style={{ margin: 0, backgroundColor: headerBackgroundColor }} />
      </Grid>
      <Grid container spacing={2} px={4} py={4}>
        {children}
      </Grid>
    </>
  );
};

export default Page;
