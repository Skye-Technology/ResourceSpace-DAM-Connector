# SkyeTechnology.Umbraco.ResourceSpace

Bring Your Own DAM to **Umbraco 17+**: search, preview, and pick **ResourceSpace** assets directly in the Bellissima backoffice and deliver responsive renditions to **Razor views** and the **Content Delivery API** without copying files into `wwwroot/media`.

Built and maintained by **[Skye Technology](https://skye.technology)**.

## Installation

```bash
dotnet add package SkyeTechnology.Umbraco.ResourceSpace
```

Services, Management API endpoints, Property Value Converters, and backoffice extensions are registered automatically via `ResourceSpaceComposer`.

## Configuration (`appsettings.json` / Environment Variables)

```json
{
  "ResourceSpace": {
    "BaseUrl": "https://dam.example.org",
    "PublicBaseUrl": null,
    "ApiUser": "umbraco_api",
    "ApiKey": "<configure-via-user-secrets-or-key-vault>",
    "DefaultDeliverySize": "scr",
    "BlockRestrictedAssets": true,
    "AssetCacheDuration": "00:05:00",
    "MissingAssetCacheDuration": "00:00:30"
  }
}
```

## Razor Usage

Add the TagHelper to `Views/_ViewImports.cshtml`:

```cshtml
@using SkyeTechnology.Umbraco.ResourceSpace.Models
@addTagHelper *, SkyeTechnology.Umbraco.ResourceSpace
```

Render responsive DAM assets in any view:

```cshtml
@{
    var hero = Model.Value<PublishedDamAsset>("heroAsset");
}
<dam-image asset="@hero"
           size="scr"
           sizes="(max-width: 980px) 100vw, 980px"
           loading="eager" />
```

## License

MIT — Copyright (c) 2026 [Skye Technology](https://skye.technology).
