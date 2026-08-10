using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;

namespace Xpense.API.Infrastructure;

public interface ICurrentUser
{
    Guid Id { get; }

    bool IsAuthenticated { get; }
}

public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true &&
        TryGetId(out _);

    public Guid Id
    {
        get
        {
            if (TryGetId(out var id))
                return id;

            throw new InvalidOperationException("The current request has no authenticated user identifier");
        }
    }

    private bool TryGetId(out Guid id)
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out id);
    }
}
