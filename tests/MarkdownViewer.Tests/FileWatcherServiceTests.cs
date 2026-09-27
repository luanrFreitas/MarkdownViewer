using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MarkdownViewer.Services;
using Xunit;

namespace MarkdownViewer.Tests;

public class FileWatcherServiceTests
{
    private static string CreateTempMarkdownFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mdviewer-test-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, "# inicial");
        return path;
    }

    [Fact]
    public async Task RapidConsecutiveWrites_RaiseSingleDebouncedDocumentChanged()
    {
        var path = CreateTempMarkdownFile();
        try
        {
            using var watcher = new FileWatcherService(path);
            var changedCount = 0;
            watcher.DocumentChanged += (_, _) => Interlocked.Increment(ref changedCount);

            for (var i = 0; i < 5; i++)
            {
                File.WriteAllText(path, $"# versão {i}");
                await Task.Delay(30);
            }

            await Task.Delay(800); // aguarda o debounce (350ms) + margem

            Assert.Equal(1, changedCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FileDeleted_RaisesDocumentUnavailable_NotDocumentChanged()
    {
        var path = CreateTempMarkdownFile();
        using var watcher = new FileWatcherService(path);
        var unavailableRaised = false;
        var changedRaised = false;
        watcher.DocumentUnavailable += (_, _) => unavailableRaised = true;
        watcher.DocumentChanged += (_, _) => changedRaised = true;

        File.Delete(path);
        await Task.Delay(800);

        Assert.True(unavailableRaised);
        Assert.False(changedRaised);
    }

    [Fact]
    public async Task FileDeleted_RaisesDocumentUnavailable_ImmediatelyWithoutWaitingForDebounce()
    {
        // T032/contracts/render-pipeline.md: Deleted não deve esperar os 350ms de debounce de Changed.
        var path = CreateTempMarkdownFile();
        using var watcher = new FileWatcherService(path);
        var tcs = new TaskCompletionSource();
        watcher.DocumentUnavailable += (_, _) => tcs.TrySetResult();

        var stopwatch = Stopwatch.StartNew();
        File.Delete(path);
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        stopwatch.Stop();

        Assert.Same(tcs.Task, completed);
        Assert.True(stopwatch.ElapsedMilliseconds < 300,
            $"DocumentUnavailable levou {stopwatch.ElapsedMilliseconds}ms para disparar (esperado: quase imediato, bem abaixo dos 350ms de debounce).");
    }

    [Fact]
    public async Task FileRenamedOverWithSameName_IsTreatedAsChanged_NotUnavailable()
    {
        // Save atômico (write-temp + rename-sobre-o-original): o arquivo observado continua existindo.
        var path = CreateTempMarkdownFile();
        using var watcher = new FileWatcherService(path);
        var changedRaised = false;
        var unavailableRaised = false;
        watcher.DocumentChanged += (_, _) => changedRaised = true;
        watcher.DocumentUnavailable += (_, _) => unavailableRaised = true;

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, "# conteúdo via save atômico");
        File.Move(tempPath, path, overwrite: true);

        await Task.Delay(800);

        Assert.True(changedRaised);
        Assert.False(unavailableRaised);
    }
}
