using System;
using System.Diagnostics;
using System.Text;
using MarkdownViewer.Services;
using Xunit;

namespace MarkdownViewer.Tests;

public class MarkdownRendererTests
{
    [Fact]
    public void Render_OneMegabyteDocument_CompletesWithinPerformanceBudget()
    {
        // T026 / SC-001: abrir e renderizar até 1MB deve ficar bem abaixo do orçamento de 1s ponta-a-ponta
        // (este teste mede só o parsing+render, que é a parte dominante do custo).
        var sb = new StringBuilder();
        var i = 0;
        while (sb.Length < 1_000_000)
        {
            i++;
            sb.Append($"## Seção {i}\n\nTexto de exemplo com **negrito** e `codigo`.\n\n")
              .Append("| A | B |\n|---|---|\n| 1 | 2 |\n\n")
              .Append("```csharp\nvar x = 1;\n```\n\n");
        }

        var content = sb.ToString();
        MarkdownRenderer.Render(content, @"C:\docs"); // warm-up: exclui custo de JIT/inicialização do pipeline da medição

        var stopwatch = Stopwatch.StartNew();
        var html = MarkdownRenderer.Render(content, @"C:\docs");
        stopwatch.Stop();

        Assert.NotEmpty(html);
        Assert.True(stopwatch.ElapsedMilliseconds < 900,
            $"Render de ~1MB levou {stopwatch.ElapsedMilliseconds}ms (orçamento: <900ms para deixar margem de UI dentro do SC-001 de <1s).");
    }

    [Fact]
    public void Render_EmptyContent_ReturnsEmptyStateHtml()
    {
        var html = MarkdownRenderer.Render("", @"C:\docs");

        Assert.Contains("md-empty-state", html);
    }

    [Fact]
    public void Render_WhitespaceOnlyContent_ReturnsEmptyStateHtml()
    {
        var html = MarkdownRenderer.Render("   \n  ", @"C:\docs");

        Assert.Contains("md-empty-state", html);
    }

    [Fact]
    public void Render_Table_ProducesHtmlTable()
    {
        var markdown = "| A | B |\n|---|---|\n| 1 | 2 |\n";

        var html = MarkdownRenderer.Render(markdown, @"C:\docs");

        Assert.Contains("<table>", html);
        Assert.Contains("<td>1</td>", html);
    }

    [Fact]
    public void Render_TaskList_ProducesCheckboxInput()
    {
        var markdown = "- [x] done\n- [ ] pending\n";

        var html = MarkdownRenderer.Render(markdown, @"C:\docs");

        Assert.Contains("type=\"checkbox\"", html);
    }

    [Fact]
    public void Render_CodeBlockWithLanguage_ProducesLanguageClass()
    {
        var markdown = "```csharp\nvar x = 1;\n```";

        var html = MarkdownRenderer.Render(markdown, @"C:\docs");

        Assert.Contains("language-csharp", html);
    }

    [Fact]
    public void Render_RelativeImagePath_ResolvesAgainstBaseDirectory()
    {
        var markdown = "![alt](imagens/foto.png)";

        var html = MarkdownRenderer.Render(markdown, @"C:\notas\projeto");

        // T035: resolve para o host virtual mapeado por MainWindow para baseDirectory, não file://.
        Assert.Contains("https://mdviewer.local/imagens/foto.png", html);
    }

    [Fact]
    public void Render_RawHtmlImageTag_ResolvesRelativeSrcAgainstBaseDirectory()
    {
        // T031/FR-003: <img> escrito como HTML bruto (comum p/ controlar tamanho) não vira LinkInline.
        var markdown = "Texto com <img src=\"imagens/foto.png\" width=\"200\"> no meio.";

        var html = MarkdownRenderer.Render(markdown, @"C:\notas\projeto");

        Assert.Contains("https://mdviewer.local/imagens/foto.png", html);
        Assert.Contains("width=\"200\"", html);
    }

    [Fact]
    public void Render_RelativeImagePath_OutsideBaseDirectory_FallsBackToFileUri()
    {
        // Fora da árvore de baseDirectory o host virtual não alcança — melhor esforço via file://.
        var markdown = "![alt](../outra-pasta/foto.png)";

        var html = MarkdownRenderer.Render(markdown, @"C:\notas\projeto");

        Assert.Contains("file:///C:/notas/outra-pasta/foto.png", html);
    }

    [Fact]
    public void Render_RawHtmlAnchorTag_LeavesAbsoluteHrefUnchanged()
    {
        var markdown = "<a href=\"https://example.com\">site</a>";

        var html = MarkdownRenderer.Render(markdown, @"C:\docs");

        Assert.Contains("href=\"https://example.com\"", html);
    }

    [Fact]
    public void Render_AbsoluteHttpLink_IsLeftUnchanged()
    {
        var markdown = "[site](https://example.com)";

        var html = MarkdownRenderer.Render(markdown, @"C:\docs");

        Assert.Contains("href=\"https://example.com", html);
    }

    [Fact]
    public void Render_RelativeMarkdownLink_ResolvesToVirtualHostUrl()
    {
        var markdown = "[outro](outro.md)";

        var html = MarkdownRenderer.Render(markdown, @"C:\notas");

        Assert.Contains("https://mdviewer.local/outro.md", html);
    }

    [Fact]
    public void Render_NeverThrows_ForArbitraryInput()
    {
        var weird = "# Título\n<script>alert(1)</script>\n[[[malformed(((";

        var exception = Record.Exception(() => MarkdownRenderer.Render(weird, @"C:\docs"));

        Assert.Null(exception);
    }
}
