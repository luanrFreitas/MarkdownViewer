using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MarkdownViewer.Services;
using Xunit;

namespace MarkdownViewer.Tests;

/// <summary>
/// T034 / SC-003: mede a latência ponta-a-ponta do live reload — salvar o arquivo em disco até o
/// conteúdo re-renderizado estar pronto — sem depender da UI (WPF/WebView2), que não é testável
/// automatizadamente neste MVP (plan.md: "Testing").
/// </summary>
public class LiveReloadLatencyTests
{
    [Fact]
    public async Task SavingWatchedFile_ProducesRenderedContentWithinTwoSeconds()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mdviewer-latency-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, "# versão inicial");

        using var watcher = new FileWatcherService(path);
        var tcs = new TaskCompletionSource<string>();

        watcher.DocumentChanged += (_, _) =>
        {
            try
            {
                var raw = File.ReadAllText(path);
                var html = MarkdownRenderer.Render(raw, Path.GetDirectoryName(path)!);
                tcs.TrySetResult(html);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        };

        try
        {
            var stopwatch = Stopwatch.StartNew();
            File.WriteAllText(path, "# versão atualizada\n\nConteúdo novo salvo pelo usuário.");

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            stopwatch.Stop();

            Assert.Same(tcs.Task, completed);
            var html = await tcs.Task;
            Assert.Contains("versão atualizada", html);
            Assert.True(stopwatch.ElapsedMilliseconds < 2000,
                $"Live reload ponta-a-ponta levou {stopwatch.ElapsedMilliseconds}ms (orçamento SC-003: <2000ms).");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
