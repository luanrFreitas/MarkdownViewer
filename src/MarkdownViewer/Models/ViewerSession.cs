using System.IO;
using MarkdownViewer.Services;

namespace MarkdownViewer.Models;

/// <summary>Uma janela aberta do visualizador, associada a no máximo um MarkdownDocument por vez (data-model.md).</summary>
public sealed class ViewerSession
{
    public MarkdownDocument? CurrentDocument { get; private set; }

    /// <summary>Observador do arquivo atualmente aberto. Nulo até o primeiro documento ser carregado; recriado a cada troca de documento (US3, T022).</summary>
    public FileWatcherService? Watcher { get; private set; }

    public string WindowTitle =>
        CurrentDocument is null
            ? "Markdown Viewer"
            : $"{Path.GetFileName(CurrentDocument.FilePath)} — Markdown Viewer";

    /// <summary>Substitui o documento atual e descarta o watcher anterior (FR-004; data-model.md, regra de transição).</summary>
    public void SetDocument(MarkdownDocument document, FileWatcherService? watcher)
    {
        Watcher?.Dispose();
        CurrentDocument = document;
        Watcher = watcher;
    }

    public void MarkUnavailable()
    {
        Watcher?.Dispose();
        Watcher = null;
    }
}
