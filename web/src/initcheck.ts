import { accountSchema } from "./typings";
import { schema } from "./typings/forms/ITransactionFormData";

if (!accountSchema || !schema) throw new Error("a schema failed to build");

console.log("MODULE INIT OK");
