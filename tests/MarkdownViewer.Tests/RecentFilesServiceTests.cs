using System.IO;
using System.Linq;
using MarkdownViewer.Services;
using Microsoft.Win32;
using Xunit;

namespace MarkdownViewer.Tests;

/// <summary>
/// RecentFilesService grava em HKEY_CURRENT_USER\Software\MarkdownViewer\RecentFiles (registro real,
/// mesmo padrão de FileAssociationService). Cada teste salva e restaura esse valor para não poluir
/// a lista de recentes de verdade do usuário nem interferir entre testes.
/// </summary>
public class RecentFilesServiceTests : IDisposable
{
    private const string RegistryKeyPath = @"Software\MarkdownViewer\RecentFiles";
    private readonly string?[] _originalValues = new string?[15];

    public RecentFilesServiceTests()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
        for (var i = 0; i < _originalValues.Length; i++)
        {
            _originalValues[i] = key?.GetValue(i.ToString()) as string;
        }
    }

    public void Dispose()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
        for (var i = 0; i < _originalValues.Length; i++)
        {
            if (_originalValues[i] is { } value)
            {
                key.SetValue(i.ToString(), value);
            }
            else
            {
                key.DeleteValue(i.ToString(), throwOnMissingValue: false);
            }
        }
    }

    private static string CreateTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mdviewer-recent-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, "# teste");
        return path;
    }

    [Fact]
    public void Add_PutsMostRecentFileFirst()
    {
        var a = CreateTempFile();
        var b = CreateTempFile();
        try
        {
            RecentFilesService.Add(a);
            RecentFilesService.Add(b);

            var recents = RecentFilesService.GetExisting();

            Assert.Equal(b, recents[0]);
            Assert.Equal(a, recents[1]);
        }
        finally
        {
            File.Delete(a);
            File.Delete(b);
        }
    }

    [Fact]
    public void Add_SameFileTwice_DoesNotDuplicate()
    {
        var a = CreateTempFile();
        try
        {
            RecentFilesService.Add(a);
            RecentFilesService.Add(a);

            var recents = RecentFilesService.GetExisting();

            Assert.Single(recents.Where(p => string.Equals(p, Path.GetFullPath(a), StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            File.Delete(a);
        }
    }

    [Fact]
    public void GetExisting_PrunesFilesThatNoLongerExist()
    {
        var stillThere = CreateTempFile();
        var deleted = CreateTempFile();
        try
        {
            RecentFilesService.Add(deleted);
            RecentFilesService.Add(stillThere);
            File.Delete(deleted);

            var recents = RecentFilesService.GetExisting();

            Assert.DoesNotContain(recents, p => string.Equals(p, Path.GetFullPath(deleted), StringComparison.OrdinalIgnoreCase));
            Assert.Contains(recents, p => string.Equals(p, Path.GetFullPath(stillThere), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(stillThere);
        }
    }

    [Fact]
    public void Add_MoreThanMax_KeepsOnlyMostRecentFifteen()
    {
        var files = Enumerable.Range(0, 18).Select(_ => CreateTempFile()).ToList();
        try
        {
            foreach (var f in files)
            {
                RecentFilesService.Add(f);
            }

            var recents = RecentFilesService.GetExisting();

            Assert.Equal(15, recents.Count);
            Assert.Equal(Path.GetFullPath(files[^1]), recents[0]);
        }
        finally
        {
            foreach (var f in files)
            {
                File.Delete(f);
            }
        }
    }
}
