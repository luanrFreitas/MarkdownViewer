using System.IO;

namespace MarkdownViewer.Models;

/// <summary>Documento Markdown atualmente aberto (data-model.md).</summary>
public sealed class MarkdownDocument
{
    /// <summary>Caminho absoluto do arquivo. MUST existir no momento da abertura (contracts/cli-invocation.md).</summary>
    public string FilePath { get; }

    /// <summary>Pasta que contém FilePath; usada para resolver imagens/links relativos (FR-003). Sempre derivada, nunca setada diretamente.</summary>
    public string BaseDirectory { get; }

    public string RawContent { get; set; } = string.Empty;

    public string RenderedHtml { get; set; } = string.Empty;

    public DateTime LastModifiedUtc { get; set; }

    public MarkdownDocument(string filePath)
    {
        FilePath = Path.GetFullPath(filePath);
        BaseDirectory = Path.GetDirectoryName(FilePath) ?? string.Empty;
    }
}
