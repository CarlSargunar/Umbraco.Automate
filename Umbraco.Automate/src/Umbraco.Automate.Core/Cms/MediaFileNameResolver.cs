using Microsoft.AspNetCore.StaticFiles;

namespace Umbraco.Automate.Core.Cms;

/// <summary>
/// Works out the file name to store a downloaded media file under.
/// <para>
/// A URL is not a file picker: it may end in a slash, carry a query string, or name a route
/// with no extension at all. Umbraco's upload property validates the extension against the
/// allowed file types, so a name with no extension is stored but then reads as an invalid
/// file — hence the fall back to the response's content type before giving up.
/// </para>
/// <para>
/// Sanitising the result is deliberately not done here: <c>SetValue</c> already runs the name
/// through <c>IShortStringHelper.CleanStringForSafeFileName</c> before storing it, and doing it
/// twice would only risk the two rules disagreeing.
/// </para>
/// </summary>
internal static class MediaFileNameResolver
{
    /// <summary>
    /// ASP.NET Core's extension-to-content-type table — the same one the CMS uses to serve
    /// back-office graphics. Only ever asked questions in its own direction: whether a name
    /// carries a usable extension, and whether a candidate extension really is the type in hand.
    /// </summary>
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    /// <summary>
    /// Content types whose extension is nothing like their subtype, so <see cref="ResolveExtension"/>
    /// cannot get them to name themselves.
    /// <para>
    /// Kept short on purpose. Scanning the framework table backwards would cover more types but
    /// answers badly where one type has many extensions: <c>text/plain</c> comes back as
    /// <c>.asm</c>, because no tiebreak picks <c>.txt</c> out of the dozens registered. A short
    /// list of types a media download actually produces beats a long list of wrong answers, and
    /// anything absent simply keeps the name it already had.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string> ExtensionsByContentType =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["text/plain"] = ".txt",
            ["audio/mpeg"] = ".mp3",
            // The table knows .zip only as application/x-zip-compressed, so the IANA-registered
            // type a real server sends matches nothing without this.
            ["application/zip"] = ".zip",
        };

    /// <summary>
    /// Resolves a file name from the URL's last path segment, falling back to
    /// <paramref name="fallbackName"/> when the URL carries none. When the resulting name has
    /// no extension, one is appended from <paramref name="contentType"/> if it can be worked out.
    /// </summary>
    /// <param name="uri">The URL the file was downloaded from.</param>
    /// <param name="fallbackName">The media item's name, used when the URL names no file.</param>
    /// <param name="contentType">The response's media type, without parameters.</param>
    public static string Resolve(Uri uri, string? fallbackName, string? contentType)
    {
        // LocalPath excludes the query string and is already percent-decoded, so the only
        // thing left to handle is a trailing slash — without the trim, a URL ending in one
        // would name no file at all and fall back unnecessarily.
        var candidate = Path.GetFileName(uri.LocalPath.TrimEnd('/'));

        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = string.IsNullOrWhiteSpace(fallbackName) ? "file" : fallbackName;
        }

        // Asking the table whether the name resolves to a known type, rather than
        // Path.HasExtension, which is too loose: it calls the ".2" in "v1.2" an extension and
        // would leave that file with no usable one.
        if (ContentTypes.TryGetContentType(candidate, out _))
        {
            return candidate;
        }

        var extension = ResolveExtension(contentType);
        return extension is null ? candidate : candidate + extension;
    }

    /// <summary>
    /// Finds the extension for a content type by having the type name it.
    /// <para>
    /// <c>image/jpeg</c> suggests <c>.jpeg</c>, and asking the table confirms that really is
    /// <c>image/jpeg</c>, so the answer verifies itself and no tiebreak is needed. Types that
    /// cannot name themselves fall back to <see cref="ExtensionsByContentType"/>, and anything
    /// neither covers gets no extension rather than a guess.
    /// </para>
    /// </summary>
    private static string? ResolveExtension(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var slash = contentType.IndexOf('/');
        if (slash >= 0)
        {
            // "image/svg+xml" names ".svg" — the "+xml" is a structuring suffix, not part of it.
            var subtype = contentType[(slash + 1)..];
            var plus = subtype.IndexOf('+');
            if (plus > 0)
            {
                subtype = subtype[..plus];
            }

            var suggested = "." + subtype;
            if (ContentTypes.TryGetContentType("_" + suggested, out var roundTripped)
                && string.Equals(roundTripped, contentType, StringComparison.OrdinalIgnoreCase))
            {
                return suggested;
            }
        }

        return ExtensionsByContentType.TryGetValue(contentType, out var known) ? known : null;
    }
}
