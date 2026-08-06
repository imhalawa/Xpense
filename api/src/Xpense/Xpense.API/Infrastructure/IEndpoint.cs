using Microsoft.AspNetCore.Routing;

namespace Xpense.API.Infrastructure;

public interface IEndpoint
{
    static abstract void Map(IEndpointRouteBuilder app);
}
