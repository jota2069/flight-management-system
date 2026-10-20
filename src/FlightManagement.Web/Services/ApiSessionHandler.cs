using FlightManagement.Security;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FlightManagement.Web.Services;

// No CookieContainer or user-specific fields: HttpClient handlers may be pooled.
public sealed class ApiSessionHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var context = accessor.HttpContext;
        if (context is not null)
        {
            // Cookie authentication may chunk large tickets; forward only our cookie family.
            var cookies = context.Request.Cookies.Where(c =>
                c.Key == FlightSecurity.SessionCookie || c.Key == FlightSecurity.CsrfCookie ||
                (c.Key.StartsWith(FlightSecurity.SessionCookie + "C", StringComparison.Ordinal) &&
                 int.TryParse(c.Key.AsSpan((FlightSecurity.SessionCookie + "C").Length), out _)));
            var header = string.Join("; ", cookies.Select(c => c.Key + "=" + c.Value));
            if (header.Length > 0) request.Headers.TryAddWithoutValidation("Cookie", header);
            if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head)
            {
                string? token = context.Request.Headers[FlightSecurity.CsrfHeader].FirstOrDefault();
                if (token is null && context.Request.HasFormContentType)
                    token = (await context.Request.ReadFormAsync(cancellationToken))["__RequestVerificationToken"].FirstOrDefault();
                if (token is not null) request.Headers.TryAddWithoutValidation(FlightSecurity.CsrfHeader, token);
            }
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
