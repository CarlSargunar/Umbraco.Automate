using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class CreateMediaActionTests
{
    private readonly Mock<IMediaService> _mediaService = new();
    private readonly Mock<IMediaTypeService> _mediaTypeService = new();
    private readonly Mock<IUserIdKeyResolver> _userIdKeyResolver = new();
    private readonly Mock<IBackOfficeSecurityAccessor> _securityAccessor = new();
    private readonly Mock<IUmbracoContextFactory> _contextFactory = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();
    private readonly CreateMediaAction _action;

    public CreateMediaActionTests()
    {
        _userIdKeyResolver.Setup(x => x.GetAsync(It.IsAny<Guid>())).ReturnsAsync(-1);

        _contextFactory
            .Setup(x => x.EnsureUmbracoContext())
            .Returns(new UmbracoContextReference(
                Mock.Of<IUmbracoContext>(),
                isRoot: false,
                Mock.Of<IUmbracoContextAccessor>()));

        _authorizer
            .Setup(a => a.AuthorizeMediaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _action = new CreateMediaAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _mediaService.Object,
            _mediaTypeService.Object,
            _userIdKeyResolver.Object,
            _securityAccessor.Object,
            _contextFactory.Object,
            _authorizer.Object,
            Mock.Of<ILogger<CreateMediaAction>>());
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.createMedia");

    [Fact]
    public async Task ExecuteAsync_EmptyMediaType_ReturnsValidationError()
    {
        var context = CreateContext(new CreateMediaSettings
        {
            ParentKey = Guid.NewGuid().ToString(),
            MediaType = "",
            Name = "New Image",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_MediaTypeIsNotAKey_ReturnsValidationError()
    {
        var context = CreateContext(new CreateMediaSettings
        {
            ParentKey = Guid.NewGuid().ToString(),
            MediaType = "Image",
            Name = "New Image",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyName_ReturnsValidationError()
    {
        var context = CreateContext(new CreateMediaSettings
        {
            ParentKey = Guid.NewGuid().ToString(),
            MediaType = Guid.NewGuid().ToString(),
            Name = "",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidParentKey_ReturnsValidationError()
    {
        var context = CreateContext(new CreateMediaSettings
        {
            ParentKey = "not-a-guid",
            MediaType = Guid.NewGuid().ToString(),
            Name = "New Image",
        });

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ParentNotFound_ReturnsParentNotFoundOutcome()
    {
        var parentKey = Guid.NewGuid();
        var mediaTypeKey = Guid.NewGuid();
        _mediaService.Setup(x => x.GetById(parentKey)).Returns((IMedia?)null);

        var context = CreateContext(
            new CreateMediaSettings
            {
                ParentKey = parentKey.ToString(),
                MediaType = mediaTypeKey.ToString(),
                Name = "New Image",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(CreateMediaAction.OutcomeParentNotFound);

        var output = result.OutputData as CreateMediaOutput;
        output.ShouldNotBeNull();
        output.MediaTypeKey.ShouldBe(mediaTypeKey);
    }

    [Fact]
    public async Task ExecuteAsync_MediaTypeNotFound_ReturnsMediaTypeNotFoundOutcome()
    {
        var parentKey = Guid.NewGuid();
        var mediaTypeKey = Guid.NewGuid();
        _mediaService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IMedia>());
        _mediaTypeService.Setup(x => x.Get(mediaTypeKey)).Returns((IMediaType?)null);

        var context = CreateContext(
            new CreateMediaSettings
            {
                ParentKey = parentKey.ToString(),
                MediaType = mediaTypeKey.ToString(),
                Name = "New Image",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(CreateMediaAction.OutcomeMediaTypeNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_VariantMediaTypeWithoutCulture_ReturnsValidationError()
    {
        var parentKey = Guid.NewGuid();
        var mediaTypeKey = Guid.NewGuid();
        _mediaService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IMedia>());

        var mediaType = new Mock<IMediaType>();
        mediaType.SetupGet(x => x.Alias).Returns("Image");
        mediaType.SetupGet(x => x.Variations).Returns(ContentVariation.Culture);
        _mediaTypeService.Setup(x => x.Get(mediaTypeKey)).Returns(mediaType.Object);

        var context = CreateContext(
            new CreateMediaSettings
            {
                ParentKey = parentKey.ToString(),
                MediaType = mediaTypeKey.ToString(),
                Name = "New Image",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_CreatesAndSavesMedia()
    {
        var parentKey = Guid.NewGuid();
        var mediaTypeKey = Guid.NewGuid();
        var mediaKey = Guid.NewGuid();
        _mediaService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IMedia>());

        var mediaType = new Mock<IMediaType>();
        mediaType.SetupGet(x => x.Alias).Returns("Image");
        mediaType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        _mediaTypeService.Setup(x => x.Get(mediaTypeKey)).Returns(mediaType.Object);

        var created = new Mock<IMedia>();
        created.SetupGet(x => x.Key).Returns(mediaKey);
        created.SetupGet(x => x.Properties).Returns(new PropertyCollection());

        _mediaService
            .Setup(x => x.CreateMedia("New Image", parentKey, "Image", -1))
            .Returns(created.Object);
        _mediaService
            .Setup(x => x.Save(created.Object, It.IsAny<int>()))
            .Returns(Attempt<OperationResult?>.Succeed(new OperationResult(OperationResultType.Success, new EventMessages())));

        var context = CreateContext(
            new CreateMediaSettings
            {
                ParentKey = parentKey.ToString(),
                MediaType = mediaTypeKey.ToString(),
                Name = "New Image",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();

        var output = result.OutputData as CreateMediaOutput;
        output.ShouldNotBeNull();
        output.MediaKey.ShouldBe(mediaKey);
        output.Name.ShouldBe("New Image");
        output.MediaTypeKey.ShouldBe(mediaTypeKey);
        output.MediaTypeAlias.ShouldBe("Image");
        output.ParentKey.ShouldBe(parentKey);
    }

    [Fact]
    public async Task ExecuteAsync_PickerValueWithTrailingComma_ResolvesTheMediaType()
    {
        var parentKey = Guid.NewGuid();
        var mediaTypeKey = Guid.NewGuid();
        _mediaService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IMedia>());

        var mediaType = new Mock<IMediaType>();
        mediaType.SetupGet(x => x.Alias).Returns("Image");
        mediaType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        _mediaTypeService.Setup(x => x.Get(mediaTypeKey)).Returns(mediaType.Object);

        var created = new Mock<IMedia>();
        created.SetupGet(x => x.Properties).Returns(new PropertyCollection());

        _mediaService
            .Setup(x => x.CreateMedia("New Image", parentKey, "Image", -1))
            .Returns(created.Object);
        _mediaService
            .Setup(x => x.Save(created.Object, It.IsAny<int>()))
            .Returns(Attempt<OperationResult?>.Succeed(new OperationResult(OperationResultType.Success, new EventMessages())));

        var context = CreateContext(
            new CreateMediaSettings
            {
                ParentKey = parentKey.ToString(),
                MediaType = $"{mediaTypeKey},",
                Name = "New Image",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_SaveCancelledByEvent_MapsToCancelled()
    {
        var parentKey = Guid.NewGuid();
        var mediaTypeKey = Guid.NewGuid();
        _mediaService.Setup(x => x.GetById(parentKey)).Returns(Mock.Of<IMedia>());

        var mediaType = new Mock<IMediaType>();
        mediaType.SetupGet(x => x.Alias).Returns("Image");
        mediaType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        _mediaTypeService.Setup(x => x.Get(mediaTypeKey)).Returns(mediaType.Object);

        var created = new Mock<IMedia>();
        created.SetupGet(x => x.Properties).Returns(new PropertyCollection());

        _mediaService
            .Setup(x => x.CreateMedia("New Image", parentKey, "Image", -1))
            .Returns(created.Object);
        _mediaService
            .Setup(x => x.Save(created.Object, It.IsAny<int>()))
            .Returns(Attempt<OperationResult?>.Fail(new OperationResult(OperationResultType.FailedCancelledByEvent, new EventMessages())));

        var context = CreateContext(
            new CreateMediaSettings
            {
                ParentKey = parentKey.ToString(),
                MediaType = mediaTypeKey.ToString(),
                Name = "New Image",
            },
            Guid.NewGuid());

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Cancelled);
    }

    private static ActionContext CreateContext(CreateMediaSettings settings, Guid? serviceAccountKey = null)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.createMedia",
            Settings = settings,
            ExecutionContext = serviceAccountKey.HasValue
                ? new AutomationExecutionContext
                {
                    ServiceAccountKey = serviceAccountKey.Value,
                    WorkspaceId = Guid.NewGuid(),
                    WorkspaceName = "Test",
                    AutomationId = Guid.NewGuid(),
                    AutomationName = "Test",
                    RunId = Guid.NewGuid(),
                    InitiatorType = "test",
                    AllowedConnections = [],
                }
                : null,
        };
}
