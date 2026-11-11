# Bring Your Own DAM: Building a ResourceSpace Connector for Umbraco

Museums, universities, charities, and public-sector organisations rarely start a website rebuild with an empty media folder. Usually, tens of thousands of high-resolution photographs, conservation PDFs, and archival videos already live in a dedicated Digital Asset Management (DAM) system—complete with rights metadata, photographer credits, and embargo rules.

**ResourceSpace** is one of the most widely adopted open-source DAMs in those sectors. Yet when teams pair it with Umbraco, editors often end up downloading JPEGs from ResourceSpace and re-uploading them into the Umbraco Media Library. Within months, captions drift out of sync, uncropped masters bloat `wwwroot/media`, and an image restricted by an archivist in the DAM remains publicly live on the website.

In this article, we will build a clean, modern **ResourceSpace connector for Umbraco 17** step by step. Editors will search, preview, and pick ResourceSpace assets directly inside the Bellissima backoffice, while Umbraco stores only a lightweight reference and delivers live DAM renditions to both Razor views and the Content Delivery API—without duplicating a single file.

---

## 1. The ResourceSpace API and Server-Side Signing

ResourceSpace exposes a JSON HTTP endpoint at `/api/`, authenticated using **signed query strings** rather than OAuth bearer tokens. Each API user has a private scramble key, and every request must append a lowercase hexadecimal SHA-256 hash computed over `privateKey + queryString` (without the leading `?`).

Two details matter when implementing this in .NET:

1. **The private key must never reach the browser.** Because signing requires the raw API key, calling ResourceSpace directly from a backoffice web component would expose credentials in DevTools.
2. **Byte-identical query encoding.** ResourceSpace hashes the raw query string bytes it receives before `&sign=`. If `HttpClient` re-encodes spaces or UTF-8 characters differently after you compute the hash, ResourceSpace rejects the call with `Invalid signature`.

We solve deterministic signing in `ResourceSpaceRequestSigner` using `Uri.EscapeDataString` and `SHA256.HashData`:

```csharp
public static class ResourceSpaceRequestSigner
{
    public static string BuildSignedRelativeUrl(
        string apiUser,
        string privateKey,
        string functionName,
        IEnumerable<KeyValuePair<string, string?>>? parameters = null)
    {
        var sb = new StringBuilder();
        sb.Append("user=").Append(Uri.EscapeDataString(apiUser));
        sb.Append("&function=").Append(Uri.EscapeDataString(functionName));

        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
            {
                if (!string.IsNullOrWhiteSpace(key) && value is not null)
                {
                    sb.Append('&').Append(Uri.EscapeDataString(key))
                      .Append('=').Append(Uri.EscapeDataString(value));
                }
            }
        }

        var query = sb.ToString();
        var payload = Encoding.UTF8.GetBytes(string.Concat(privateKey, query));
        var sign = Convert.ToHexStringLower(SHA256.HashData(payload));

        return $"/api/?{query}&sign={sign}";
    }
}
```

Next, we place `ResourceSpaceClient` behind an Umbraco **Management API controller**. Inheriting from `ManagementApiControllerBase` automatically protects our proxy endpoints (`/umbraco/management/api/v1/resourcespace/...`) with Umbraco's OpenIddict backoffice authentication—unauthenticated requests receive `401 Unauthorized`, and the ResourceSpace key stays strictly on the server:

```csharp
[ApiVersion("1.0")]
[VersionedApiBackOfficeRoute("resourcespace")]
[ApiExplorerSettings(GroupName = "ResourceSpace DAM")]
public sealed class ResourceSpaceManagementController(IDamConnector damConnector)
    : ManagementApiControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? query = null,
        [FromQuery] string? resourceTypes = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 24,
        CancellationToken cancellationToken = default)
    {
        var result = await damConnector.SearchAsync(
            new DamSearchRequest(query, resourceTypes, skip, take),
            cancellationToken);

        return Ok(result);
    }
}
```

When wiring the connector methods behind `IDamConnector`, watch out for two real-world ResourceSpace 11 API quirks we hit during testing:
- **Inconsistent ID parameter names:** `get_resource_data`, `get_resource_field_data`, and `get_resource_all_image_sizes` expect `resource={id}`, whereas `get_resource_path` expects `ref={id}`.
- **Duplicate `original` size codes on PDFs:** For multi-format documents, `get_resource_all_image_sizes` can emit two `"size_code": "original"` entries—first the `.pdf` master and last a Ghostscript `.jpg` preview (with `"width": "?"`). Remapping that secondary preview entry to `original_preview` preserves the true `.pdf` download URL under `renditions["original"]`.

---

## 2. A Custom Property Editor in the New Backoffice

In Umbraco's Bellissima backoffice, a property editor separates its **server schema** (`propertyEditorSchema`) from its **visual component** (`propertyEditorUi`). On the C# side, `ResourceSpaceDataEditor` registers `[DataEditor("ResourceSpace.AssetPicker", ValueType = ValueTypes.Json, ValueEditorIsReusable = true)]` and implements `IValueSchemaProvider` so Umbraco knows the JSON shape stored in the database. On the client side, we declare our UI and a slide-out picker modal in `wwwroot/App_Plugins/ResourceSpace/umbraco-package.json`:

```json
{
  "name": "ResourceSpace DAM Connector",
  "version": "1.0.0",
  "extensions": [
    {
      "type": "propertyEditorUi",
      "alias": "ResourceSpace.PropertyEditorUi.AssetPicker",
      "name": "ResourceSpace Asset Picker",
      "element": "/App_Plugins/ResourceSpace/resourcespace-connector.js",
      "elementName": "resourcespace-picker",
      "meta": {
        "label": "ResourceSpace Asset Picker",
        "icon": "icon-picture",
        "group": "media",
        "propertyEditorSchemaAlias": "ResourceSpace.AssetPicker"
      }
    },
    {
      "type": "modal",
      "alias": "ResourceSpace.Modal.AssetPicker",
      "name": "ResourceSpace Asset Picker Modal",
      "element": "/App_Plugins/ResourceSpace/resourcespace-connector.js",
      "elementName": "resourcespace-picker-modal"
    }
  ]
}
```

Our TypeScript API helper consumes `UMB_AUTH_CONTEXT` to attach the current editor's bearer token when calling our Management API controller. Inside `<resourcespace-picker>`, extending `UmbLitElement` lets us open the modal via `UMB_MODAL_MANAGER_CONTEXT` and notify Umbraco whenever the value changes by dispatching `new UmbChangeEvent()`:

```typescript
private async _openPicker() {
  const modalManager = await this.getContext(UMB_MODAL_MANAGER_CONTEXT);
  if (!modalManager) return;

  const modal = modalManager.open(this, RESOURCESPACE_PICKER_MODAL, {
    data: { allowedResourceTypes: this._allowedResourceTypes },
    value: { selection: null },
  });

  const result = await modal.onSubmit().catch(() => undefined);
  if (!result?.selection) return;

  this.value = {
    resourceId: result.selection.resourceId,
    provider: result.selection.provider,
    title: result.selection.title,
    altText: this.value?.altText ?? null,
    fileExtension: result.selection.fileExtension,
    resourceType: result.selection.resourceType,
    width: result.selection.width,
    height: result.selection.height,
    credit: result.selection.credit,
  };
  this.dispatchEvent(new UmbChangeEvent());
}
```

Inside `<resourcespace-picker-modal>`, editors get debounced keyword search (`search_get_previews`), resource-type filter pills (`Photo`, `Document`, `Video`), a responsive `<uui-card-media>` thumbnail grid, and a metadata inspector listing dimensions, copyright credits, and generated ResourceSpace renditions (`thm`, `pre`, `scr`, `original`) before confirming their choice.

---

## 3. What to Store: Keeping the DAM as Single Source of Truth

A common mistake in DAM integrations is persisting full rendition URLs in Umbraco's property JSON. If your infrastructure team later migrates the DAM domain, adds a CDN, or regenerates watermarks, thousands of content nodes silently break until republished.

Instead, we store only a compact `DamAssetReference`:

```json
{
  "resourceId": "1",
  "provider": "resourcespace",
  "title": "Roman Floor Mosaic, North Wing",
  "altText": "Overhead view of the fourth-century geometric Roman mosaic",
  "fileExtension": "jpg",
  "resourceType": "Photo",
  "width": 1600,
  "height": 1000,
  "credit": "Trustees of the Civic Museum"
}
```

Why keep those few extra strings alongside `resourceId`?

- **`altText` belongs to editorial context.** The DAM holds a general archivist caption, whereas accessibility requires describing why an image appears on *this* page. If the editor leaves `altText` blank, we fall back to ResourceSpace's caption automatically.
- **Resilient degradation.** Lightweight snapshot fields (`title`, `width`, `height`, `credit`) preserve layout geometry and backoffice labels even if the DAM is briefly undergoing maintenance.

---

## 4. Rendering and Delivery: Razor and Headless

To turn that stored reference into something views and frontend apps can consume without knowing ResourceSpace internals, we implement both `PropertyValueConverterBase` and `IDeliveryApiPropertyValueConverter` on `ResourceSpacePropertyValueConverter`:

```csharp
public sealed class ResourceSpacePropertyValueConverter(
    IDamConnector damConnector,
    IOptionsMonitor<ResourceSpaceOptions> optionsMonitor,
    ILogger<ResourceSpacePropertyValueConverter> logger)
    : PropertyValueConverterBase, IDeliveryApiPropertyValueConverter
{
    public override bool IsConverter(IPublishedPropertyType propertyType)
        => propertyType.EditorAlias.InvariantEquals(ResourceSpaceDataEditor.EditorAlias);

    public override Type GetPropertyValueType(IPublishedPropertyType propertyType)
        => typeof(PublishedDamAsset);

    public override PropertyCacheLevel GetPropertyCacheLevel(IPublishedPropertyType propertyType)
        => PropertyCacheLevel.None;

    public object? ConvertIntermediateToDeliveryApiObject(
        IPublishedElement owner, IPublishedPropertyType propertyType,
        PropertyCacheLevel referenceCacheLevel, object? inter, bool preview, bool expanding)
        => ResolvePublishedAsset(propertyType, inter as DamAssetReference);
}
```

Notice `PropertyCacheLevel.None`. In Umbraco 17, returning `None` ensures the property value is not frozen indefinitely inside published content snapshots; instead, freshness is governed by our connector's `IMemoryCache` policy.

When resolving a `PublishedDamAsset`, the converter distinguishes between **images** and **documents**:
- For images (including museum master TIFFs), `Url` resolves to the configured web rendition (defaulting to `scr`, typically 1280px JPEG) and `SrcSet` assembles every width-qualified image rendition (`200w`, `864w`, `1280w`, `1600w`).
- For documents like PDFs—where ResourceSpace generates Ghostscript JPEG previews alongside the original PDF—`Url` points to the `.pdf` file while `PreviewUrl` exposes the cover thumbnail.

In Razor, a small `<dam-image>` TagHelper wires `src`, `srcset`, `sizes`, `alt`, and layout-stabilising `width`/`height` attributes in one line:

```cshtml
@{
    var hero = Model.Value<PublishedDamAsset>("heroAsset");
}
<dam-image asset="@hero"
           size="scr"
           sizes="(max-width: 980px) 100vw, 980px"
           loading="eager" />
```

And because we implemented `IDeliveryApiPropertyValueConverter`, headless Next.js or Astro frontends querying `/umbraco/delivery/api/v2/content` immediately receive structured JSON with `url`, `previewUrl`, `srcSet`, `isAvailable`, and the full `renditions` dictionary.

---

## 5. Production Concerns: Caching, Governance, and Configuration

Taking a DAM connector into production means planning for failure modes and asset governance:

- **Positive and negative caching.** Calling ResourceSpace on every page view would hammer the DAM. `ResourceSpaceDamConnector` caches live `DamAssetDetail` entries in `IMemoryCache` for `AssetCacheDuration` (default five minutes), while missing or restricted lookups use a shorter `MissingAssetCacheDuration` (30 seconds) so newly fixed assets recover quickly without permitting cache-miss floods. An `EvictAssetCache(resourceId)` method also supports instant webhook invalidation.
- **Deleted, archived, and restricted assets.** When an asset is purged in ResourceSpace (`get_resource_data` returns `false`), moved to an archive state (`archive != 0`), or marked restricted/confidential (`access != 0`), the connector returns `IsAvailable = false` with an `UnavailableReason` rather than leaking restricted filestore URLs. `<dam-image>` cleanly suppresses output (or renders a configured `FallbackImageUrl`), preventing broken image icons across live pages.
- **Internal vs. public URLs.** In containerised or VNet setups, Umbraco often reaches ResourceSpace over a private address (`http://resourcespace-internal`) while browsers load renditions through a public CDN (`https://dam.museum.org`). Setting `PublicBaseUrl` rewrites rendition URLs transparently.
- **Per-environment settings.** Strongly typed `ResourceSpaceOptions` bind from the `ResourceSpace` configuration section so local developers can use `appsettings.Development.json` or User Secrets while staging and production inject `ResourceSpace__ApiKey` from Azure Key Vault or environment variables.

---

## 6. Generalising the Pattern Beyond ResourceSpace

Because neither our Management API controller, our Lit modal, nor our Property Value Converter references `ResourceSpaceClient` directly, the entire integration hinges on a three-method contract:

```csharp
public interface IDamConnector
{
    string ProviderKey { get; }
    Task<DamSearchResult> SearchAsync(DamSearchRequest request, CancellationToken ct = default);
    Task<DamAssetDetail?> GetAssetAsync(string resourceId, CancellationToken ct = default);
    Task<IReadOnlyList<DamResourceTypeOption>> GetResourceTypesAsync(CancellationToken ct = default);
}
```

Swapping in another enterprise DAM means writing a single `IDamConnector` implementation:
- **Bynder:** Authenticate with OAuth 2.0 client credentials and map Bynder media derivatives into `DamRendition` entries.
- **Cloudinary:** Sign Admin Search API requests server-side and generate named transformation URLs (`f_auto,q_auto,w_800`) for `SrcSet`.
- **Azure Blob Storage:** Query blob index tags or Azure AI Search and emit front-door or short-lived read SAS URLs.

---

## Try the Starter Repository

The complete source code—including the reusable `Umbraco.Community.ResourceSpace` Razor Class Library, the TypeScript/Lit backoffice extension, xUnit tests, an Umbraco 17 demo site, and a Docker Compose stack that boots ResourceSpace 11.0 with sample museum assets—is available on GitHub under the MIT licence. Clone it, run `docker compose up -d`, `dotnet run`, and start bringing your own DAM to Umbraco.
