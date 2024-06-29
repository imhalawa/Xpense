using System.ComponentModel.DataAnnotations;
using Xpense.Services.Features.Accounts.Commands;

namespace Xpense.API.Models.Requests
{
   public class CreateAccountRequest
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