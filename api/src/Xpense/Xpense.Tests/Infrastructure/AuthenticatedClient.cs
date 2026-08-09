using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Xpense.Tests.Infrastructure;

internal sealed class AuthenticatedClient(
    string sessionCookie,
    string antiforgeryCookie,
    string requestToken) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Remove("Cookie");
        request.Headers.Add("Cookie", $"{sessionCookie}; {antiforgeryCookie}");

        if (IsMutation(request.Method) && !request.Headers.Contains("X-Xpense-Antiforgery"))
            request.Headers.Add("X-Xpense-Antiforgery", requestToken);

        return base.SendAsync(request, cancellationToken);
    }

    private static bool IsMutation(HttpMethod method) =>
        method == HttpMethod.Post ||
        method == HttpMethod.Put ||
        method == HttpMethod.Patch ||
        method == HttpMethod.Delete;
}
