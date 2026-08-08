using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;

namespace Xpense.API.Infrastructure;

public interface ICurrentUser
{
    Guid Id { get; }
}

public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid Id
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id)
                ? id
                : throw new InvalidOperationException("The current request has no authenticated user identifier");
        }
    }
}
