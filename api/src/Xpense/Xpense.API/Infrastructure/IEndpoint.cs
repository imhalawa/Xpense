using Microsoft.AspNetCore.Routing;

namespace Xpense.API.Infrastructure;

/// <summary>
/// Implemented by every slice. The member is static abstract so slices never need to be
/// instantiated or registered in DI -- discovery is a reflection scan at startup and nothing
/// else. See docs/vertical-slicing-architecture.
/// </summary>
public interface IEndpoint
{
    static abstract void Map(IEndpointRouteBuilder app);
}
