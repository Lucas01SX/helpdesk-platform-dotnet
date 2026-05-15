using System.Net;

namespace Helpdesk.Tests.Integration.Infrastructure;

// WebApplicationFactory.CreateClient() does not use a CookieContainer — Set-Cookie
// headers are visible in responses but cookies are never sent on subsequent requests.
// This handler fills that gap so integration tests can exercise cookie-based auth flows.
public sealed class CookieContainerHandler : DelegatingHandler
{
    private readonly CookieContainer _cookies = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cookieHeader = _cookies.GetCookieHeader(request.RequestUri!);
        if (!string.IsNullOrEmpty(cookieHeader))
            request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.Headers.TryGetValues("Set-Cookie", out var values))
            foreach (var value in values)
                _cookies.SetCookies(request.RequestUri!, value);

        return response;
    }
}
