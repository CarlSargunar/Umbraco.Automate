using Umbraco.Automate.Core.Cms;

namespace Umbraco.Automate.Tests.Unit.Cms;

public class MediaFileNameResolverTests
{
    [Theory]
    // The common case: the URL names the file outright.
    [InlineData("https://static.tvmaze.com/uploads/images/medium_portrait/610/1525272.jpg", "1525272.jpg")]
    // A query string must never end up in the stored name.
    [InlineData("https://example.com/photo.png?width=400&v=2", "photo.png")]
    // Percent-encoded segments are decoded so the name reads as intended.
    [InlineData("https://example.com/my%20holiday.jpg", "my holiday.jpg")]
    // A trailing slash means the last real segment names the file.
    [InlineData("https://example.com/assets/logo.svg/", "logo.svg")]
    public void Resolve_UrlNamesTheFile_UsesThatName(string url, string expected)
        => MediaFileNameResolver.Resolve(new Uri(url), "Fallback", null).ShouldBe(expected);

    [Fact]
    public void Resolve_NoExtension_AppendsOneFromContentType()
        => MediaFileNameResolver
            .Resolve(new Uri("https://example.com/images/42"), "Fallback", "image/jpeg")
            .ShouldBe("42.jpg");

    [Fact]
    public void Resolve_NoExtensionAndUnknownContentType_LeavesNameAlone()
        => MediaFileNameResolver
            .Resolve(new Uri("https://example.com/images/42"), "Fallback", "application/x-made-up")
            .ShouldBe("42");

    [Fact]
    public void Resolve_UrlNamesNoFile_FallsBackToMediaName()
        => MediaFileNameResolver
            .Resolve(new Uri("https://example.com/"), "Sunset", "image/png")
            .ShouldBe("Sunset.png");

    [Fact]
    public void Resolve_UrlNamesNoFileAndNoMediaName_UsesAPlaceholder()
        => MediaFileNameResolver
            .Resolve(new Uri("https://example.com/"), null, "image/png")
            .ShouldBe("file.png");
}
