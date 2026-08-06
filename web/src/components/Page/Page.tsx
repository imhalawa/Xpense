import { Grid, Typography } from "@mui/material";
import { ReactNode } from "react";

interface IPageProps {
  title: string;
  headerBackgroundColor?: string;
  headerColor?: string;
  children: ReactNode[] | ReactNode;
}
const Page = ({ title, children, headerBackgroundColor, headerColor }: IPageProps) => {
  return (
    <>
      <Grid
        sx={{
          backgroundColor: headerBackgroundColor,
          color: headerColor,
          borderTopLeftRadius: "1rem",
          borderTopRightRadius: "1rem",
        }}
        size={12}>
        <Typography variant="h4" sx={{
          m: 2
        }}>
          {title}
        </Typography>
        <hr style={{ margin: 0, backgroundColor: headerBackgroundColor }} />
      </Grid>
      <Grid
        container
        spacing={2}
        sx={{
          px: 4,
          pb: 4,
          pt: 0
        }}>
        {children}
      </Grid>
    </>
  );
};

export default Page;
