using System.ComponentModel.DataAnnotations;
using Xpense.Services.UseCases.Account;

namespace Xpense.Services.Models.Requests.Account
{
   public class CreateAccount
   {
      [Required]
      public required string Name { get; set; }
      
      [Required]
      public required decimal Balance { get; set; }

      public CreateAccountCommand ToCommand(){
         return new CreateAccountCommand(this.Name,this.Balance);
      }
   }
}