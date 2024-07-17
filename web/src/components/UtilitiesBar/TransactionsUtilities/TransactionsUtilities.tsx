import TransactionsForm from "../../../pages/Transactions/TransactionsForm/TransactionsForm";
import Utility from "../../Utility/Utilitiy";
import { useCalendar } from "../../../contexts/CalendarContext";
import Calendar from "../../Calendary/Calendar";

const TransactionsUtilities = () => {
  const { selectedDate } = useCalendar();
  return (
    <Utility>
      <TransactionsForm onSubmit={(transaction) => console.log(transaction)} selectedDate={selectedDate} />
      <hr />
      <Calendar />
    </Utility>
  );
};

export default TransactionsUtilities;
