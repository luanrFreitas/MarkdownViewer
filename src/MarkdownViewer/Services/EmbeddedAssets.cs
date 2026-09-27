using System.IO;
using System.Reflection;

namespace MarkdownViewer.Services;

/// <summary>Lê os assets estáticos (CSS/JS) embutidos no assembly como recursos (T004, Constitution II).</summary>
internal static class EmbeddedAssets
{
    private static readonly Lazy<string> ViewerCssLazy = new(() => ReadBySuffix("viewer.css"));
    private static readonly Lazy<string> HighlightCssLazy = new(() => ReadBySuffix("github.min.css"));
    private static readonly Lazy<string> HighlightJsLazy = new(() => ReadBySuffix("highlight.min.js"));

    public static string ViewerCss => ViewerCssLazy.Value;
    public static string HighlightCss => HighlightCssLazy.Value;
    public static string HighlightJs => HighlightJsLazy.Value;

    private static string ReadBySuffix(string fileNameSuffix)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileNameSuffix, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            return string.Empty;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return string.Empty;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
