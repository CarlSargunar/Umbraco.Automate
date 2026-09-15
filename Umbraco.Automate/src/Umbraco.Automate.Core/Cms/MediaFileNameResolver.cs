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
    /// back-office graphics. Reversing it gives ~380 content types for free rather than a
    /// hand-kept list, at the cost of the ambiguity <see cref="ExtensionOverrides"/> settles.
    /// </summary>
    private static readonly Lazy<Dictionary<string, string>> ExtensionsByContentType =
        new(BuildExtensionLookup);

    /// <summary>
    /// The handful of entries the framework table gets wrong for this purpose. Two reasons:
    /// a type that reverses to several extensions where the lowest ordinal one is not what
    /// anyone writes (<c>image/jpeg</c> would resolve to <c>.jpe</c>), and a type the table
    /// simply omits (it knows <c>.zip</c> only as <c>application/x-zip-compressed</c>, so the
    /// IANA-registered <c>application/zip</c> a real server sends finds nothing).
    /// Everything else reverses cleanly and needs no entry.
    /// </summary>
    private static readonly Dictionary<string, string> ExtensionOverrides =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/tiff"] = ".tiff",
            ["application/zip"] = ".zip",
        };

    private static Dictionary<string, string> BuildExtensionLookup()
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (extension, contentType) in new FileExtensionContentTypeProvider().Mappings)
        {
            // Lowest ordinal extension wins, so the mapping never depends on dictionary order.
            if (!lookup.TryGetValue(contentType, out var existing)
                || string.CompareOrdinal(extension, existing) < 0)
            {
                lookup[contentType] = extension;
            }
        }

        foreach (var (contentType, extension) in ExtensionOverrides)
        {
            lookup[contentType] = extension;
        }

        return lookup;
    }

    /// <summary>
    /// Resolves a file name from the URL's last path segment, falling back to
    /// <paramref name="fallbackName"/> when the URL carries none. When the resulting name has
    /// no extension, one is appended from <paramref name="contentType"/> if it is recognised.
    /// </summary>
    /// <param name="uri">The URL the file was downloaded from.</param>
    /// <param name="fallbackName">The media item's name, used when the URL names no file.</param>
    /// <param name="contentType">The response's media type, without parameters.</param>
    public static string Resolve(Uri uri, string? fallbackName, string? contentType)
    {
        // AbsolutePath rather than Segments[^1] so a query string never lands in the name.
        var candidate = Uri.UnescapeDataString(uri.AbsolutePath).TrimEnd('/');
        var lastSlash = candidate.LastIndexOf('/');
        if (lastSlash >= 0)
        {
            candidate = candidate[(lastSlash + 1)..];
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = string.IsNullOrWhiteSpace(fallbackName) ? "file" : fallbackName;
        }

        if (Path.HasExtension(candidate))
        {
            return candidate;
        }

        return contentType is not null && ExtensionsByContentType.Value.TryGetValue(contentType, out var extension)
            ? candidate + extension
            : candidate;
    }
}
