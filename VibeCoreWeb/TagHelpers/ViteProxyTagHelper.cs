using Microsoft.AspNetCore.Razor.TagHelpers;
using Vite.AspNetCore;

namespace VibeCore.TagHelpers;

/// <summary>
/// Emits same-origin Vite URLs in development so ASP.NET can proxy assets and
/// HMR through the single public preview port. Production uses the manifest.
/// </summary>
[HtmlTargetElement("script", Attributes = "vite-src")]
[HtmlTargetElement("link", Attributes = "vite-href")]
public sealed class ViteProxyTagHelper(
    IViteDevServerStatus viteStatus,
    IViteManifest manifest) : TagHelper
{
    private static readonly string DevelopmentCacheKey =
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(
            System.Globalization.CultureInfo.InvariantCulture);

    [HtmlAttributeName("vite-src")]
    public string? ViteSrc { get; set; }

    [HtmlAttributeName("vite-href")]
    public string? ViteHref { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var path = ViteSrc ?? ViteHref;
        if (string.IsNullOrWhiteSpace(path))
            return;

        var normalizedPath = path.TrimStart('~', '/');
        if (viteStatus.IsMiddlewareEnable)
        {
            var developmentUrl =
                $"/app/{normalizedPath}?v={DevelopmentCacheKey}";
            if (ViteSrc is not null)
            {
                output.Attributes.RemoveAll("vite-src");
                output.Attributes.SetAttribute("src", developmentUrl);
                output.PreElement.AppendHtml(
                    "<script type=\"module\" src=\"/app/@vite/client\"></script>\n" +
                    "<script type=\"module\">\n" +
                    "  import RefreshRuntime from '/app/@react-refresh';\n" +
                    "  RefreshRuntime.injectIntoGlobalHook(window);\n" +
                    "  window.$RefreshReg$ = () => {};\n" +
                    "  window.$RefreshSig$ = () => (type) => type;\n" +
                    "  window.__vite_plugin_react_preamble_installed__ = true;\n" +
                    "</script>\n");
            }
            else
            {
                output.Attributes.RemoveAll("vite-href");
                output.Attributes.SetAttribute("href", developmentUrl);
            }

            return;
        }

        var chunk = manifest[normalizedPath];
        if (ViteSrc is not null)
        {
            output.Attributes.RemoveAll("vite-src");
            output.Attributes.SetAttribute(
                "src",
                chunk is null ? $"/app/{normalizedPath}" : $"/app/{chunk.File}");
        }
        else
        {
            output.Attributes.RemoveAll("vite-href");
            var cssFile = chunk?.Css?.FirstOrDefault() ?? chunk?.File ?? normalizedPath;
            output.Attributes.SetAttribute("href", $"/app/{cssFile}");
        }
    }
}
