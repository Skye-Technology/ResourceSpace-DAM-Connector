using System.Text.Json;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using SkyeTechnology.Umbraco.ResourceSpace.Abstractions.Models;
using SkyeTechnology.Umbraco.ResourceSpace.PropertyEditors;

namespace SkyeTechnology.Umbraco.ResourceSpace.Demo.Seeding;

public sealed class DemoComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, DemoSchemaSeeder>();
    }
}

/// <summary>
/// Idempotently seeds the demo DataType, DocumentType, Template, and published Exhibition root page on first boot
/// so developers cloning the repo get a working end-to-end Umbraco 17 + ResourceSpace demo immediately.
/// </summary>
public sealed class DemoSchemaSeeder : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private static readonly Guid PickerDataTypeKey = Guid.Parse("7e6a9c41-4c2b-4d8e-9b15-1d3e8f2a6c90");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRuntimeState _runtimeState;
    private readonly IDataTypeService _dataTypeService;
    private readonly PropertyEditorCollection _propertyEditors;
    private readonly IConfigurationEditorJsonSerializer _configurationSerializer;
    private readonly ITemplateService _templateService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IContentService _contentService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly ILogger<DemoSchemaSeeder> _logger;

    public DemoSchemaSeeder(
        IRuntimeState runtimeState,
        IDataTypeService dataTypeService,
        PropertyEditorCollection propertyEditors,
        IConfigurationEditorJsonSerializer configurationSerializer,
        ITemplateService templateService,
        IContentTypeService contentTypeService,
        IContentService contentService,
        IShortStringHelper shortStringHelper,
        IWebHostEnvironment hostEnvironment,
        ILogger<DemoSchemaSeeder> logger)
    {
        _runtimeState = runtimeState;
        _dataTypeService = dataTypeService;
        _propertyEditors = propertyEditors;
        _configurationSerializer = configurationSerializer;
        _templateService = templateService;
        _contentTypeService = contentTypeService;
        _contentService = contentService;
        _shortStringHelper = shortStringHelper;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task HandleAsync(
        UmbracoApplicationStartedNotification notification,
        CancellationToken cancellationToken)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        var pickerDataType = await EnsurePickerDataTypeAsync();
        if (pickerDataType is null)
        {
            return;
        }

        var template = await EnsureExhibitionTemplateAsync();
        var contentType = await EnsureExhibitionContentTypeAsync(pickerDataType, template);
        EnsureDemoRootContent(contentType);
    }

    private async Task<IDataType?> EnsurePickerDataTypeAsync()
    {
        var existing = await _dataTypeService.GetAsync(PickerDataTypeKey)
                       ?? await _dataTypeService.GetAsync("ResourceSpace - Asset Picker");
        if (existing is not null)
        {
            return existing;
        }

        if (!_propertyEditors.TryGet(ResourceSpaceDataEditor.EditorAlias, out var editor) || editor is null)
        {
            _logger.LogError(
                "DataEditor '{EditorAlias}' was not registered; skipping demo schema seed.",
                ResourceSpaceDataEditor.EditorAlias);
            return null;
        }

        var dataType = new DataType(editor, _configurationSerializer)
        {
            Key = PickerDataTypeKey,
            Name = "ResourceSpace - Asset Picker",
            DatabaseType = ValueStorageType.Ntext,
            EditorUiAlias = ResourceSpaceDataEditor.EditorUiAlias,
            ConfigurationData = new Dictionary<string, object>
            {
                ["defaultRenditionSize"] = "scr",
                ["allowedResourceTypes"] = string.Empty,
                ["fallbackImageUrl"] = string.Empty,
            },
        };

        var createResult = await _dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        if (!createResult.Success)
        {
            _logger.LogError("Failed to create ResourceSpace DataType: {Status}", createResult.Status);
            return null;
        }

        _logger.LogInformation("Seeded DataType 'ResourceSpace - Asset Picker'.");
        return createResult.Result;
    }

    private async Task<ITemplate?> EnsureExhibitionTemplateAsync()
    {
        const string templateAlias = "exhibitionPage";
        var existing = await _templateService.GetAsync(templateAlias);
        if (existing is not null)
        {
            return existing;
        }

        var viewPath = Path.Combine(_hostEnvironment.ContentRootPath, "Views", "exhibitionPage.cshtml");
        var viewContent = System.IO.File.Exists(viewPath) ? await System.IO.File.ReadAllTextAsync(viewPath) : null;

        var result = await _templateService.CreateAsync(
            "Exhibition Page",
            templateAlias,
            viewContent,
            Constants.Security.SuperUserKey);

        return result.Result;
    }

    private async Task<IContentType> EnsureExhibitionContentTypeAsync(
        IDataType pickerDataType,
        ITemplate? template)
    {
        const string contentTypeAlias = "exhibitionPage";
        var existing = _contentTypeService.Get(contentTypeAlias);
        if (existing is not null)
        {
            return existing;
        }

        var textstringDataType = await _dataTypeService.GetAsync(Constants.DataTypes.Guids.TextstringGuid)
            ?? throw new InvalidOperationException("Built-in Textstring DataType not found.");

        var contentType = new ContentType(_shortStringHelper, -1)
        {
            Alias = contentTypeAlias,
            Name = "Exhibition Page",
            Icon = "icon-picture",
            AllowedAsRoot = true,
        };

        if (template is not null)
        {
            contentType.AllowedTemplates = [template];
            contentType.SetDefaultTemplate(template);
        }

        contentType.AddPropertyType(
            new PropertyType(_shortStringHelper, textstringDataType, "subtitle")
            {
                Name = "Exhibition Subtitle",
                Description = "Introductory strapline displayed below the page title.",
                SortOrder = 1,
            },
            "exhibitionDetails",
            "Exhibition Details");

        contentType.AddPropertyType(
            new PropertyType(_shortStringHelper, pickerDataType, "heroAsset")
            {
                Name = "Hero Collection Asset",
                Description = "Primary ResourceSpace image rendered with responsive srcset.",
                SortOrder = 2,
            },
            "damAssets",
            "ResourceSpace DAM Assets");

        contentType.AddPropertyType(
            new PropertyType(_shortStringHelper, pickerDataType, "secondaryAsset")
            {
                Name = "Secondary Collection Plate",
                Description = "Secondary ResourceSpace image demonstrating DAM caption fallback.",
                SortOrder = 3,
            },
            "damAssets",
            "ResourceSpace DAM Assets");

        contentType.AddPropertyType(
            new PropertyType(_shortStringHelper, pickerDataType, "cataloguePdf")
            {
                Name = "Conservation Report (PDF)",
                Description = "ResourceSpace PDF document rendered with Ghostscript cover preview and direct download URL.",
                SortOrder = 4,
            },
            "damAssets",
            "ResourceSpace DAM Assets");

        contentType.AddPropertyType(
            new PropertyType(_shortStringHelper, pickerDataType, "loanAsset")
            {
                Name = "Restricted Loan Artefact",
                Description = "References Resource #4 (access=1 in ResourceSpace) to demonstrate automatic restriction enforcement.",
                SortOrder = 5,
            },
            "damAssets",
            "ResourceSpace DAM Assets");

        await _contentTypeService.CreateAsync(contentType, Constants.Security.SuperUserKey);
        _logger.LogInformation("Seeded DocumentType 'exhibitionPage'.");
        return contentType;
    }

    private void EnsureDemoRootContent(IContentType contentType)
    {
        if (_contentService.GetRootContent().Any())
        {
            return;
        }

        var page = _contentService.Create("North Wing Roman Exhibition", Constants.System.Root, contentType.Alias);

        page.SetValue(
            "subtitle",
            "Curated assets delivered directly from ResourceSpace 11.0 via Umbraco 17 Razor and the Content Delivery API.");

        page.SetValue("heroAsset", SerializeRef(new DamAssetReference
        {
            ResourceId = "1",
            Provider = "resourcespace",
            Title = "Roman Floor Mosaic, North Wing",
            AltText = "Overhead photograph of the fourth-century geometric Roman floor mosaic in the North Wing",
            FileExtension = "jpg",
            ResourceType = "Photo",
            Width = 1600,
            Height = 1000,
            Credit = "Trustees of the Civic Museum",
        }));

        page.SetValue("secondaryAsset", SerializeRef(new DamAssetReference
        {
            ResourceId = "2",
            Provider = "resourcespace",
            Title = "Botanical Herbarium Plate — Rosa Canina",
            AltText = null, // Demonstrates fallback to DAM caption field
            FileExtension = "jpg",
            ResourceType = "Photo",
            Width = 1400,
            Height = 1000,
            Credit = "University Special Collections",
        }));

        page.SetValue("cataloguePdf", SerializeRef(new DamAssetReference
        {
            ResourceId = "3",
            Provider = "resourcespace",
            Title = "2026 Annual Conservation Report (PDF)",
            FileExtension = "pdf",
            ResourceType = "Document",
            Credit = "Heritage Conservation Trust",
        }));

        page.SetValue("loanAsset", SerializeRef(new DamAssetReference
        {
            ResourceId = "4",
            Provider = "resourcespace",
            Title = "Embargoed Loan Artefact (Restricted)",
            FileExtension = "jpg",
            ResourceType = "Photo",
            Credit = "Private Loan — Embargoed",
        }));

        _contentService.Save(page);
        _contentService.Publish(page, ["*"]);

        _logger.LogInformation("Seeded and published root content node 'North Wing Roman Exhibition'.");
    }

    private static string SerializeRef(DamAssetReference reference)
        => JsonSerializer.Serialize(reference, JsonOptions);
}
