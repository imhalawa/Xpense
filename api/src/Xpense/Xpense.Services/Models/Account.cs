using System.ComponentModel.DataAnnotations;

namespace Xpense.Services.Models
{
    public record CreateAccount(
        [property: Required] string Name,
        [property: Range(0, double.MaxValue)] decimal Balance
    );
}