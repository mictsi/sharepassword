using System.Net;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace SharePassword.Services;

/// <summary>
/// Helpers that write an untrusted <c>returnUrl</c> (sourced from the request
/// query string) into a page safely. Each value is first validated as a local,
/// relative URL to prevent open redirects, then HTML-encoded so it cannot break
/// out of the surrounding attribute and inject markup or script (cross-site
/// scripting). The encoded result is returned as pre-rendered
/// <see cref="IHtmlContent"/> so Razor writes it verbatim instead of encoding it
/// a second time.
/// </summary>
public static class ReturnUrlHtmlExtensions
{
    /// <summary>
    /// Returns a validated, HTML-encoded local return URL for use as an HTML
    /// attribute value, for example a <c>data-*</c> attribute that client script
    /// reads back. Non-local or missing values collapse to an empty string.
    /// </summary>
    public static IHtmlContent SafeReturnUrlAttribute(this IUrlHelper url, string? returnUrl)
    {
        var localReturnUrl = url.IsLocalUrl(returnUrl) ? returnUrl! : string.Empty;
        return new HtmlString(WebUtility.HtmlEncode(localReturnUrl));
    }

    /// <summary>
    /// Builds a link to <paramref name="action"/> that forwards a validated local
    /// return URL as a query-string parameter. The generated href is HTML-encoded.
    /// Non-local or missing return URLs are omitted from the link.
    /// </summary>
    public static IHtmlContent ActionWithReturnUrl(this IUrlHelper url, string action, string? returnUrl)
    {
        var localReturnUrl = url.IsLocalUrl(returnUrl) ? returnUrl : null;
        var href = url.Action(action, localReturnUrl is null ? null : new { returnUrl = localReturnUrl }) ?? "#";
        return new HtmlString(WebUtility.HtmlEncode(href));
    }
}
