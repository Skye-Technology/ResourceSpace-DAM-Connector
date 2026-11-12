using Microsoft.AspNetCore.Razor.TagHelpers;
using SkyeTechnology.Umbraco.ResourceSpace.Internal;
using SkyeTechnology.Umbraco.ResourceSpace.Models;

namespace SkyeTechnology.Umbraco.ResourceSpace.TagHelpers;

/// <summary>
/// Renders a responsive <c>&lt;img&gt;</c> tag from a <see cref="PublishedDamAsset"/>,
/// automatically wiring <c>srcset</c>, <c>sizes</c>, <c>alt</c>, and CLS-preventing dimensions,
/// or suppressing output cleanly when the DAM asset has been deleted or restricted.
/// </summary>
[HtmlTargetElement("dam-image", TagStructure = TagStructure.WithoutEndTag)]
public sealed class DamImageTagHelper : TagHelper
{
    public PublishedDamAsset? Asset { get; set; }

    public string Size { get; set; } = "scr";

    public string Sizes { get; set; } = "(max-width: 768px) 100vw, 800px";

    public string Loading { get; set; } = "lazy";

    public string? CssClass { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var resolvedUrl = DamSecurityGuards.SanitizeHttpOrRelativeUrl(Asset?.GetRenditionUrl(Size));
        if (Asset is null || string.IsNullOrWhiteSpace(resolvedUrl))
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "img";
        output.TagMode = TagMode.SelfClosing;
        output.Attributes.SetAttribute("src", resolvedUrl);
        output.Attributes.SetAttribute("alt", Asset.AltText);
        output.Attributes.SetAttribute("loading", Loading);
        output.Attributes.SetAttribute("decoding", "async");

        if (!string.IsNullOrWhiteSpace(Asset.SrcSet) && !Asset.IsFallback)
        {
            output.Attributes.SetAttribute("srcset", Asset.SrcSet);
            output.Attributes.SetAttribute("sizes", Sizes);
        }

        if (Asset.Width is > 0 && Asset.Height is > 0)
        {
            output.Attributes.SetAttribute("width", Asset.Width.Value);
            output.Attributes.SetAttribute("height", Asset.Height.Value);
        }

        if (!string.IsNullOrWhiteSpace(CssClass))
        {
            output.Attributes.SetAttribute("class", CssClass);
        }
    }
}
