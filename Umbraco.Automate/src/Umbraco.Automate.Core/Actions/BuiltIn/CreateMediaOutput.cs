namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="CreateMediaAction"/>.
/// </summary>
public sealed class CreateMediaOutput
{
    /// <summary>Gets the key of the created media item. <see cref="Guid.Empty"/> when creation did not happen.</summary>
    public Guid MediaKey { get; init; }

    /// <summary>Gets the name that was requested for the new media item.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the media type alias that was requested.</summary>
    public string MediaTypeAlias { get; init; } = string.Empty;

    /// <summary>Gets the key of the parent media item the new item was created under.</summary>
    public Guid ParentKey { get; init; }
}
