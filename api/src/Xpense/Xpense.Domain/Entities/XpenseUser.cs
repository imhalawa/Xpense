using Microsoft.AspNetCore.Identity;
using Xpense.Domain.Enums;

namespace Xpense.Domain.Entities;

public class XpenseUser : IdentityUser<Guid>
{
    public AccountState State { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public void Touch() => UpdatedAt = DateTime.UtcNow;
}
