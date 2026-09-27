using System.Windows;

namespace MarkdownViewer;

/// <summary>
/// Ponto de entrada. Interpreta 0 ou 1 argumento de linha de comando (caminho do .md) e
/// inicializa a MainWindow (contracts/cli-invocation.md). Cada invocação do executável
/// cria uma janela/processo independente — não há singleton entre instâncias (FR-011).
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string? filePath = e.Args.Length > 0 ? e.Args[0] : null;

        var window = new MainWindow(filePath);
        window.Show();
    }
}
