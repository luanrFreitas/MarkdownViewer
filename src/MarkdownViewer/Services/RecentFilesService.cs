using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace MarkdownViewer.Services;

/// <summary>
/// Lista de arquivos .md abertos recentemente (mais recente primeiro), persistida em
/// HKEY_CURRENT_USER\Software\MarkdownViewer\RecentFiles — mesmo padrão de armazenamento
/// (registro, sem privilégio elevado) já usado por FileAssociationService.
/// </summary>
public static class RecentFilesService
{
    private const int MaxEntries = 15;
    private const string RegistryKeyPath = @"Software\MarkdownViewer\RecentFiles";

    /// <summary>Registra um arquivo como o mais recentemente aberto, removendo duplicatas.</summary>
    public static void Add(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var list = ReadRaw()
            .Where(p => !string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        list.Insert(0, fullPath);
        if (list.Count > MaxEntries)
        {
            list = list.Take(MaxEntries).ToList();
        }

        Write(list);
    }

    /// <summary>
    /// Retorna os arquivos recentes que ainda existem em disco (mais recente primeiro).
    /// Remove silenciosamente da lista persistida qualquer arquivo que não exista mais.
    /// </summary>
    public static IReadOnlyList<string> GetExisting()
    {
        var raw = ReadRaw();
        var existing = raw.Where(File.Exists).ToList();

        if (existing.Count != raw.Count)
        {
            Write(existing);
        }

        return existing;
    }

    private static List<string> ReadRaw()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
        if (key is null)
        {
            return new List<string>();
        }

        var result = new List<string>();
        for (var i = 0; i < MaxEntries; i++)
        {
            if (key.GetValue(i.ToString()) is string path && !string.IsNullOrEmpty(path))
            {
                result.Add(path);
            }
        }

        return result;
    }

    private static void Write(List<string> list)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
        for (var i = 0; i < MaxEntries; i++)
        {
            if (i < list.Count)
            {
                key.SetValue(i.ToString(), list[i]);
            }
            else
            {
                key.DeleteValue(i.ToString(), throwOnMissingValue: false);
            }
        }
    }
}
