import { Box, Button, Card, CardActions, CardContent } from "@mui/material";
import { DateCalendar } from "@mui/x-date-pickers";
import { useCalendar } from "../../contexts/CalendarContext";

const Calendar = () => {
  const { selectedDate, setSelectedDate } = useCalendar();
  return (
    <Card>
      <CardContent>
        <DateCalendar
          sx={{ width: "90%" }}
          value={selectedDate}
          onChange={(date) => setSelectedDate(date)}
          disableFuture
        />
      </CardContent>
      <CardActions>
        <Button onClick={() => setSelectedDate(null)} fullWidth>
          Clear
        </Button>
      </CardActions>
    </Card>
  );
};

export default Calendar;
