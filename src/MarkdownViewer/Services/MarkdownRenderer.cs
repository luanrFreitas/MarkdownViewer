using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdigDocument = Markdig.Syntax.MarkdownDocument;

namespace MarkdownViewer.Services;

/// <summary>
/// Converte Markdown (GFM) em um HTML completo e autocontido, pronto para WebView2.NavigateToString
/// (contracts/render-pipeline.md). Resolve caminhos relativos de imagens/links contra baseDirectory (FR-003).
/// </summary>
public static class MarkdownRenderer
{
    /// <summary>
    /// Host virtual mapeado por MainWindow para a pasta do documento aberto via
    /// CoreWebView2.SetVirtualHostNameToFolderMapping (T035). Permite que conteúdo exibido via
    /// NavigateToString referencie imagens/links locais sem escrever nenhum arquivo em disco e
    /// sem esbarrar no bloqueio do Chromium a recursos file:// a partir de uma origem opaca.
    /// </summary>
    public const string VirtualHostName = "mdviewer.local";

    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    /// <summary>HTML de estado vazio inicial, quando o app abre sem nenhum arquivo (App.xaml.cs / MainWindow).</summary>
    public static string RenderNoDocument() =>
        WrapPage("<div class=\"md-empty-state\"><p>Arraste um arquivo .md aqui ou pressione Ctrl+O para abrir.</p></div>");

    /// <summary>HTML de erro amigável para arquivo inexistente/ilegível (T016, contracts/cli-invocation.md).</summary>
    public static string RenderError(string message) =>
        WrapPage($"<div class=\"md-error-state\"><p>{WebUtility.HtmlEncode(message)}</p></div>");

    /// <summary>HTML exibido quando o arquivo aberto deixa de existir em disco (T024, spec.md Edge Cases).</summary>
    public static string RenderUnavailable(string fileName) =>
        WrapPage($"<div class=\"md-error-state\"><p>\"{WebUtility.HtmlEncode(fileName)}\" não está mais disponível (foi movido ou excluído).</p></div>");

    public static string Render(string? rawMarkdown, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(rawMarkdown))
        {
            return WrapPage("<div class=\"md-empty-state\"><p>Este arquivo está vazio.</p></div>");
        }

        try
        {
            MarkdigDocument document = Markdig.Markdown.Parse(rawMarkdown, Pipeline);
            RewriteRelativeUrls(document, baseDirectory);

            using var writer = new StringWriter();
            var renderer = new Markdig.Renderers.HtmlRenderer(writer);
            Pipeline.Setup(renderer);
            renderer.Render(document);

            return WrapPage($"<div class=\"md-content\">{writer}</div>");
        }
        catch (Exception)
        {
            // FR-009: nunca lançar para o chamador — cair para o texto bruto escapado.
            var escaped = WebUtility.HtmlEncode(rawMarkdown);
            return WrapPage($"<pre class=\"md-content\">{escaped}</pre>");
        }
    }

    private static readonly Regex RawHtmlUrlAttribute =
        new(@"(?<attr>\bsrc|\bhref)\s*=\s*(?<quote>[""'])(?<url>[^""']*)\k<quote>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static void RewriteRelativeUrls(MarkdigDocument document, string baseDirectory)
    {
        foreach (var link in document.Descendants<LinkInline>())
        {
            link.Url = ResolveUrl(link.Url, baseDirectory);
        }

        // T031/FR-003: <img>/<a> escritos como HTML bruto (comum p/ controlar tamanho de imagem)
        // não viram LinkInline — o Markdig só preserva o texto da tag. Reescrevemos src/href aqui também.
        foreach (var html in document.Descendants<HtmlInline>())
        {
            html.Tag = RawHtmlUrlAttribute.Replace(html.Tag, match =>
            {
                var resolved = ResolveUrl(match.Groups["url"].Value, baseDirectory);
                return $"{match.Groups["attr"].Value}={match.Groups["quote"].Value}{resolved}{match.Groups["quote"].Value}";
            });
        }
    }

    /// <summary>
    /// Deixa URLs absolutas (http/https/mailto/data) e âncoras (#...) intactas; resolve tudo o mais
    /// contra baseDirectory como uma URL do host virtual (https://mdviewer.local/...), que MainWindow
    /// mantém mapeado para baseDirectory (T035) — evita escrever qualquer arquivo em disco e evita o
    /// bloqueio do Chromium a file:// a partir de uma origem opaca (NavigateToString).
    /// </summary>
    private static string ResolveUrl(string? url, string baseDirectory)
    {
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(baseDirectory))
        {
            return url ?? string.Empty;
        }

        if (url.StartsWith('#') || Uri.IsWellFormedUriString(url, UriKind.Absolute))
        {
            return url;
        }

        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(baseDirectory, url.Replace('/', Path.DirectorySeparatorChar)));
            var relativePath = Path.GetRelativePath(baseDirectory, fullPath);

            if (relativePath.StartsWith("..") || Path.IsPathRooted(relativePath))
            {
                // Fora da árvore de baseDirectory (ex.: "../outra-pasta/img.png") — o host virtual só
                // cobre baseDirectory. Mantém file:// como melhor esforço (funciona para navegação de
                // página inteira; sub-recursos como <img> podem não carregar a partir do NavigateToString).
                return new Uri(fullPath).AbsoluteUri;
            }

            var segments = relativePath.Replace(Path.DirectorySeparatorChar, '/').Split('/').Select(Uri.EscapeDataString);
            return $"https://{VirtualHostName}/{string.Join('/', segments)}";
        }
        catch (Exception)
        {
            // Caminho inválido: preserva o texto original em vez de quebrar a renderização (FR-009).
            return url;
        }
    }

    private static string WrapPage(string bodyHtml)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html><head><meta charset=\"utf-8\">");
        sb.Append("<style>").Append(EmbeddedAssets.ViewerCss).Append("</style>");
        sb.Append("<style>").Append(EmbeddedAssets.HighlightCss).Append("</style>");
        sb.Append("</head><body>");
        sb.Append(bodyHtml);
        sb.Append("<script>").Append(EmbeddedAssets.HighlightJs).Append("</script>");
        sb.Append("<script>if (window.hljs) { hljs.highlightAll(); }</script>");
        sb.Append("</body></html>");
        return sb.ToString();
    }
}
