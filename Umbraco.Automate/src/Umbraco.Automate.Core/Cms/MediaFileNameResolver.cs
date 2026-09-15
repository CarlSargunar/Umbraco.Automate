namespace Umbraco.Automate.Core.Cms;

/// <summary>
/// Works out the file name to store a downloaded media file under.
/// <para>
/// A URL is not a file picker: it may end in a slash, carry a query string, or name a route
/// with no extension at all. Umbraco's upload property validates the extension against the
/// allowed file types, so a name with no extension is stored but then reads as an invalid
/// file — hence the fall back to the response's content type before giving up.
/// </para>
/// </summary>
internal static class MediaFileNameResolver
{
    /// <summary>
    /// Content types worth mapping back to an extension. Deliberately short: these are the
    /// types a media automation realistically downloads, and an unrecognised one falls through
    /// to the URL's own name rather than guessing.
    /// </summary>
    private static readonly Dictionary<string, string> ExtensionsByContentType =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/gif"] = ".gif",
            ["image/webp"] = ".webp",
            ["image/avif"] = ".avif",
            ["image/svg+xml"] = ".svg",
            ["image/tiff"] = ".tiff",
            ["image/bmp"] = ".bmp",
            ["application/pdf"] = ".pdf",
        };

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

        return contentType is not null && ExtensionsByContentType.TryGetValue(contentType, out var extension)
            ? candidate + extension
            : candidate;
    }
}
