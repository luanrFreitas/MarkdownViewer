using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using MarkdownViewer.Models;
using MarkdownViewer.Services;

namespace MarkdownViewer;

public partial class MainWindow : Window
{
    private readonly ViewerSession _session = new();
    private readonly string? _pendingInitialPath;
    private string? _tempHtmlPath;
    private string? _mappedBaseDirectory;

    // NavigateToString tem um teto prático não documentado (~2MB) acima do qual pode falhar
    // silenciosamente (T030). Documentos maiores são gravados em %TEMP% e exibidos via Navigate —
    // o host virtual (T035) resolve as imagens/links independentemente de onde o .html mora.
    private const int NavigateToStringSafeLimit = 1_500_000;

    public MainWindow(string? initialFilePath)
    {
        InitializeComponent();
        _pendingInitialPath = initialFilePath;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await WebView.EnsureCoreWebView2Async();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Falha ao inicializar o WebView2 Runtime: {ex.Message}",
                "Markdown Viewer", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown(1);
            return;
        }

        WebView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
        WebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

        // AllowExternalDrop=false deixa a área do WebView2 sem NENHUM drop target registrado
        // (é uma janela nativa própria hospedada dentro da nossa — WPF não assume esse papel por
        // ela) — resulta no cursor de "proibido" ao arrastar. Mantendo o padrão (true), o próprio
        // WebView2 trata o drop como uma navegação para o arquivo solto, que o NavigationStarting
        // abaixo já intercepta para .md/.markdown (FR-004/T017).

        if (_pendingInitialPath is not null)
        {
            await OpenDocumentAsync(_pendingInitialPath);
        }
        else
        {
            DisplayHtml(MarkdownRenderer.RenderNoDocument());
        }
    }

    // T035: em vez de gravar um arquivo .html na pasta do documento (efeito colateral indesejado —
    // arquivo órfão em caso de encerramento anormal, dentro de uma pasta que pode ser um repositório
    // git ou sincronizada na nuvem), mapeamos um host virtual para baseDirectory e usamos
    // NavigateToString normalmente. O host virtual resolve as imagens/links (MarkdownRenderer já gera
    // URLs https://mdviewer.local/...) sem escrever nada em disco e sem o bloqueio do Chromium a
    // file:// a partir de uma origem opaca.
    private void DisplayHtml(string html, string? baseDirectory = null)
    {
        if (!string.IsNullOrEmpty(baseDirectory) && Directory.Exists(baseDirectory) && baseDirectory != _mappedBaseDirectory)
        {
            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                MarkdownRenderer.VirtualHostName, baseDirectory, CoreWebView2HostResourceAccessKind.Allow);
            _mappedBaseDirectory = baseDirectory;
        }

        // T030: NavigateToString tem um teto prático de tamanho; acima dele, gravamos em %TEMP% e
        // navegamos via file:// — o host virtual acima resolve as imagens/links independentemente
        // de onde o .html do documento em si está sendo servido.
        CleanupTempHtmlFile();
        if (html.Length <= NavigateToStringSafeLimit)
        {
            WebView.CoreWebView2.NavigateToString(html);
            return;
        }

        _tempHtmlPath = Path.Combine(Path.GetTempPath(), $"mdviewer-{Guid.NewGuid():N}.html");
        File.WriteAllText(_tempHtmlPath, html);
        WebView.CoreWebView2.Navigate(new Uri(_tempHtmlPath).AbsoluteUri);
    }

    private void CleanupTempHtmlFile()
    {
        if (_tempHtmlPath is null)
        {
            return;
        }

        try
        {
            File.Delete(_tempHtmlPath);
        }
        catch (IOException)
        {
            // Melhor esforço: o arquivo temporário não é crítico e o SO limpa %TEMP% eventualmente.
        }

        _tempHtmlPath = null;
    }

    private async Task OpenDocumentAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                ShowError($"Arquivo não encontrado: {path}");
                return;
            }

            string raw;
            try
            {
                raw = await File.ReadAllTextAsync(path);
            }
            catch (IOException ioEx)
            {
                ShowError($"Não foi possível ler o arquivo (pode estar em uso por outro programa): {ioEx.Message}");
                return;
            }
            catch (UnauthorizedAccessException)
            {
                ShowError("Sem permissão para ler este arquivo.");
                return;
            }

            var document = new MarkdownDocument(path)
            {
                RawContent = raw,
                LastModifiedUtc = DateTime.UtcNow,
            };

            var html = await Task.Run(() => MarkdownRenderer.Render(raw, document.BaseDirectory));
            document.RenderedHtml = html;

            var watcher = new FileWatcherService(path);
            watcher.DocumentChanged += Watcher_DocumentChanged;
            watcher.DocumentUnavailable += Watcher_DocumentUnavailable;

            _session.SetDocument(document, watcher);
            Title = _session.WindowTitle;
            DisplayHtml(html, document.BaseDirectory);
        }
        catch (Exception ex)
        {
            ShowError($"Erro inesperado ao abrir o arquivo: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        Title = "Markdown Viewer";
        DisplayHtml(MarkdownRenderer.RenderError(message));
    }

    // T023: alteração salva no arquivo aberto -> re-renderizar dentro do orçamento de 2s (FR-005, SC-003).
    private void Watcher_DocumentChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(async () => await ReloadCurrentDocumentAsync()));
    }

    private async Task ReloadCurrentDocumentAsync()
    {
        var doc = _session.CurrentDocument;
        if (doc is null)
        {
            return;
        }

        try
        {
            var raw = await File.ReadAllTextAsync(doc.FilePath);
            var html = await Task.Run(() => MarkdownRenderer.Render(raw, doc.BaseDirectory));
            doc.RawContent = raw;
            doc.RenderedHtml = html;
            doc.LastModifiedUtc = DateTime.UtcNow;
            DisplayHtml(html, doc.BaseDirectory);
        }
        catch (Exception)
        {
            // Leitura coincidiu com uma escrita ainda em andamento; a próxima notificação do watcher tenta de novo.
        }
    }

    // T024: arquivo removido/movido enquanto aberto -> mensagem amigável, sem fechar a janela (spec.md Edge Cases).
    private void Watcher_DocumentUnavailable(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var fileName = _session.CurrentDocument is not null
                ? Path.GetFileName(_session.CurrentDocument.FilePath)
                : "arquivo";
            DisplayHtml(MarkdownRenderer.RenderUnavailable(fileName));
            _session.MarkUnavailable();
        }));
    }

    // T012: ação explícita de associação de arquivo (FR-001, nunca automática).
    private void SetAsDefaultButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            FileAssociationService.Register();
            MessageBox.Show(
                "Markdown Viewer definido como visualizador padrão de .md e .markdown.",
                "Markdown Viewer", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Não foi possível registrar a associação de arquivo: {ex.Message}",
                "Markdown Viewer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // T018: seletor de arquivos (Ctrl+O / botão) — FR-004.
    private void OpenFileButton_Click(object sender, RoutedEventArgs e) => OpenFileViaDialog();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OpenFileViaDialog();
            e.Handled = true;
        }
    }

    private void OpenFileViaDialog()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Arquivos Markdown (*.md;*.markdown)|*.md;*.markdown|Todos os arquivos (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) == true)
        {
            _ = OpenDocumentAsync(dialog.FileName);
        }
    }

    // T017: arrastar-e-soltar — FR-004.
    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        var mdFile = files.FirstOrDefault(f =>
            f.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase));

        if (mdFile is not null)
        {
            _ = OpenDocumentAsync(mdFile);
        }
    }

    // T015: links http(s) abrem no navegador padrão; links relativos para outro .md abrem no próprio viewer (FR-008).
    private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (TryInterceptLink(e.Uri))
        {
            e.Cancel = true;
        }
    }

    // O WebView2 abre um popup nativo feio por padrão sempre que um clique "pede" uma nova janela
    // (ex.: navegação para um file:// que o Chromium decide não fazer inline). Este app nunca deve
    // ter janelas filhas — ou tratamos o link (externo/interno) ou simplesmente descartamos o pedido.
    private void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        TryInterceptLink(e.Uri);
    }

    /// <returns>true se o link foi tratado (externo no navegador padrão, ou .md aberto no próprio viewer).</returns>
    private bool TryInterceptLink(string? uri)
    {
        if (string.IsNullOrEmpty(uri) || uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Link relativo para outro .md servido pelo host virtual (T035) — mapeado para o
        // baseDirectory do documento atual. MUST ser checado ANTES da regra genérica de
        // http(s) abaixo, já que https://mdviewer.local/... também começa com "https://".
        var virtualPrefix = $"https://{MarkdownRenderer.VirtualHostName}/";
        if (uri.StartsWith(virtualPrefix, StringComparison.OrdinalIgnoreCase) && _mappedBaseDirectory is not null)
        {
            var relativePath = Uri.UnescapeDataString(new Uri(uri).AbsolutePath.TrimStart('/'));
            if (relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                relativePath.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
            {
                var localPath = Path.Combine(_mappedBaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                _ = OpenDocumentAsync(localPath);
                return true;
            }

            return false;
        }

        if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            return true;
        }

        // Recurso fora da árvore de baseDirectory (ResolveUrl caiu para file:// como melhor esforço).
        if (uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var localPath = new Uri(uri).LocalPath;
            if (localPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
                localPath.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
            {
                _ = OpenDocumentAsync(localPath);
                return true;
            }
        }

        return false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _session.Watcher?.Dispose();
        CleanupTempHtmlFile();
        base.OnClosed(e);
    }
}
