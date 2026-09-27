using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MarkdownViewer.Services;

/// <summary>
/// Associa .md/.markdown ao executável via HKEY_CURRENT_USER\Software\Classes (sem privilégio elevado).
/// MUST ser chamado apenas por ação explícita do usuário — nunca automaticamente no startup
/// (FR-001, spec.md Clarifications 2026-09-27, contracts/cli-invocation.md).
/// </summary>
public static class FileAssociationService
{
    private const string ProgId = "MarkdownViewer.Document";
    private static readonly string[] Extensions = { ".md", ".markdown" };

    public static void Register()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Não foi possível determinar o caminho do executável atual.");

        foreach (var extension in Extensions)
        {
            using var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}");
            extKey.SetValue(string.Empty, ProgId);
        }

        using var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}");
        progIdKey.SetValue(string.Empty, "Markdown Document");

        // T033: ícone do app nos arquivos .md associados no Explorer (não só no .exe em si).
        using var iconKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\DefaultIcon");
        iconKey.SetValue(string.Empty, $"{exePath},0");

        using var commandKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\shell\open\command");
        commandKey.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");

        NotifyShellOfChange();
    }

    /// <summary>Faz o Explorer reconhecer a nova associação sem precisar reiniciar (SHChangeNotify).</summary>
    private static void NotifyShellOfChange()
    {
        const int SHCNE_ASSOCCHANGED = 0x08000000;
        const int SHCNF_IDLIST = 0x0000;
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
