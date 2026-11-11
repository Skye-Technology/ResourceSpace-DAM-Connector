using System.Text.Json.Nodes;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;

namespace SkyeTechnology.Umbraco.ResourceSpace.PropertyEditors;

/// <summary>
/// Registers the <c>ResourceSpace.AssetPicker</c> data editor schema in Umbraco 17.
/// Stores a lightweight JSON <see cref="Abstractions.Models.DamAssetReference"/> in the database
/// while delegating UI rendering to the Lit backoffice component (<c>ResourceSpace.PropertyEditorUi.AssetPicker</c>).
/// </summary>
[DataEditor(
    EditorAlias,
    ValueEditorIsReusable = true,
    ValueType = ValueTypes.Json)]
public sealed class ResourceSpaceDataEditor : DataEditor, IValueSchemaProvider
{
    public const string EditorAlias = "ResourceSpace.AssetPicker";
    public const string EditorUiAlias = "ResourceSpace.PropertyEditorUi.AssetPicker";

    public ResourceSpaceDataEditor(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
    {
        SupportsReadOnly = true;
        DefaultConfiguration = new Dictionary<string, object>
        {
            ["defaultRenditionSize"] = "scr",
            ["allowedResourceTypes"] = string.Empty,
            ["fallbackImageUrl"] = string.Empty,
        };
    }

    public Type? GetValueType(object? configuration) => typeof(object);

    public JsonObject? GetValueSchema(object? configuration) => new()
    {
        ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
        ["type"] = new JsonArray("object", "null"),
        ["properties"] = new JsonObject
        {
            ["resourceId"] = new JsonObject { ["type"] = "string" },
            ["provider"] = new JsonObject { ["type"] = "string" },
            ["title"] = new JsonObject { ["type"] = new JsonArray("string", "null") },
            ["altText"] = new JsonObject { ["type"] = new JsonArray("string", "null") },
            ["fileExtension"] = new JsonObject { ["type"] = new JsonArray("string", "null") },
            ["resourceType"] = new JsonObject { ["type"] = new JsonArray("string", "null") },
            ["width"] = new JsonObject { ["type"] = new JsonArray("integer", "null") },
            ["height"] = new JsonObject { ["type"] = new JsonArray("integer", "null") },
            ["credit"] = new JsonObject { ["type"] = new JsonArray("string", "null") },
        },
        ["required"] = new JsonArray("resourceId"),
    };
}
