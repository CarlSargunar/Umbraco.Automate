using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Security;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that creates a new media item under a parent in Umbraco CMS. Media has
/// no draft / published split, so the item is live as soon as it is saved — no follow-up
/// publish step is needed.
/// </summary>
[Action("umbracoAutomate.createMedia", "Create Media",
    Description = "Creates a new media item under a parent in Umbraco CMS.",
    Group = "Media",
    Icon = "icon-add",
    RequiredSections = [UmbracoConstants.Applications.Media])]
public sealed class CreateMediaAction : ActionBase<CreateMediaSettings, CreateMediaOutput>, ICmsAction
{
    /// <summary>
    /// Outcome emitted when the parent media item does not exist.
    /// </summary>
    public const string OutcomeParentNotFound = "parentNotFound";

    /// <summary>
    /// Outcome emitted when the media type alias doesn't resolve to a real media type.
    /// </summary>
    public const string OutcomeMediaTypeNotFound = "mediaTypeNotFound";

    private readonly IMediaService _mediaService;
    private readonly IMediaTypeService _mediaTypeService;
    private readonly IUserIdKeyResolver _userIdKeyResolver;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly ILogger<CreateMediaAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateMediaAction"/> class.
    /// </summary>
    public CreateMediaAction(
        ActionInfrastructure infrastructure,
        IMediaService mediaService,
        IMediaTypeService mediaTypeService,
        IUserIdKeyResolver userIdKeyResolver,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoContextFactory umbracoContextFactory,
        IAutomationActionAuthorizer authorizer,
        ILogger<CreateMediaAction> logger)
        : base(infrastructure)
    {
        _mediaService = mediaService;
        _mediaTypeService = mediaTypeService;
        _userIdKeyResolver = userIdKeyResolver;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _umbracoContextFactory = umbracoContextFactory;
        _authorizer = authorizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<CreateMediaSettings>();

        if (string.IsNullOrWhiteSpace(settings.MediaTypeAlias))
        {
            return ActionResult.Failed(
                new ArgumentException("Media type alias is required."),
                StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            return ActionResult.Failed(
                new ArgumentException("Name is required."),
                StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.ParentKey) || !Guid.TryParse(settings.ParentKey, out var parentKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing parent key: '{settings.ParentKey}'."),
                StepRunErrorCategory.Validation);
        }

        if (await _authorizer.AuthorizeMediaOrFailAsync(parentKey, cancellationToken) is { } failure)
        {
            return failure;
        }

        var parent = _mediaService.GetById(parentKey);
        if (parent is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Parent media {ParentKey} not found.",
                context.AutomationId, context.RunId, parentKey);

            return SuccessWithOutcome(OutcomeParentNotFound, new CreateMediaOutput
            {
                Name = settings.Name,
                MediaTypeAlias = settings.MediaTypeAlias,
                ParentKey = parentKey,
            });
        }

        var mediaType = _mediaTypeService.Get(settings.MediaTypeAlias);
        if (mediaType is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Media type {MediaTypeAlias} not found.",
                context.AutomationId, context.RunId, settings.MediaTypeAlias);

            return SuccessWithOutcome(OutcomeMediaTypeNotFound, new CreateMediaOutput
            {
                Name = settings.Name,
                MediaTypeAlias = settings.MediaTypeAlias,
                ParentKey = parentKey,
            });
        }

        var variesByCulture = (mediaType.Variations & ContentVariation.Culture) != 0;
        if (variesByCulture && string.IsNullOrWhiteSpace(settings.Culture))
        {
            return ActionResult.Failed(
                new ArgumentException($"Culture is required because media type '{settings.MediaTypeAlias}' varies by culture."),
                StepRunErrorCategory.Validation);
        }

        var userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key
            ?? context.ExecutionContext?.ServiceAccountKey
            ?? throw new InvalidOperationException("No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");

        var userId = await _userIdKeyResolver.GetAsync(userKey);

        var media = _mediaService.CreateMedia(settings.Name, parentKey, mediaType.Alias, userId);

        if (variesByCulture)
        {
            media.SetCultureName(settings.Name, settings.Culture!);
        }

        ApplyProperties(media, settings.PropertiesJson);

        // Required when running from the outbox dispatcher, which has no HTTP request
        // scope. The save raises notifications (e.g. webhook delivery) that resolve
        // media URLs via UrlProvider, which requires an UmbracoContext.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var result = _mediaService.Save(media, userId);

        if (result.Success)
        {
            return Success(new CreateMediaOutput
            {
                MediaKey = media.Key,
                Name = settings.Name,
                MediaTypeAlias = mediaType.Alias,
                ParentKey = parentKey,
            });
        }

        var status = result.Result?.Result ?? OperationResultType.FailedExceptionThrown;
        return ActionResult.Failed(
            new InvalidOperationException($"Failed to save new media under '{parentKey}': {status}"),
            MapErrorCategory(status));
    }

    /// <summary>
    /// Applies optional invariant property values from a JSON object. Malformed JSON and
    /// unknown property aliases are silently skipped — this is optional convenience config,
    /// not a required part of creating the media item.
    /// </summary>
    private static void ApplyProperties(IMedia media, string? propertiesJson)
    {
        if (string.IsNullOrWhiteSpace(propertiesJson))
        {
            return;
        }

        Dictionary<string, string>? properties;
        try
        {
            properties = JsonSerializer.Deserialize<Dictionary<string, string>>(propertiesJson);
        }
        catch (JsonException)
        {
            return;
        }

        if (properties is null)
        {
            return;
        }

        foreach (var (alias, value) in properties)
        {
            if (media.Properties.Contains(alias))
            {
                media.SetValue(alias, value);
            }
        }
    }

    private static StepRunErrorCategory MapErrorCategory(OperationResultType status) => status switch
    {
        OperationResultType.FailedCancelledByEvent => StepRunErrorCategory.Cancelled,
        OperationResultType.FailedCannot => StepRunErrorCategory.Validation,
        OperationResultType.FailedExceptionThrown => StepRunErrorCategory.Unknown,
        _ => StepRunErrorCategory.Unknown,
    };
}
