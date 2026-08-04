using Xpense.Services.Features.Accounts.Commands;

namespace Xpense.API.Models.Requests
{
   public class CreateAccountRequest
   {
      public required string Name { get; set; }
      public required decimal Balance { get; set; }

      public CreateAccountCommand ToCommand(){
         return new CreateAccountCommand(this.Name,this.Balance);
      }
   }
}