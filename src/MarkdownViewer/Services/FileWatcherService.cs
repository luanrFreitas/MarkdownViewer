using System.IO;
using System.Threading;

namespace MarkdownViewer.Services;

/// <summary>
/// Observa o arquivo do MarkdownDocument aberto e notifica mudanças (FR-005, contracts/render-pipeline.md).
/// Usa dois debounces: um normal para coalescer múltiplos eventos de um único save (~350ms), e um
/// bem mais curto (~100ms) para sinais de indisponibilidade (Deleted/Renamed-para-fora) — rápido o
/// suficiente para não parecer "preso" no debounce de Changed, mas ainda absorve o padrão comum de
/// save atômico (write-temp + rename-sobre-o-original), onde um Deleted passageiro é seguido por um
/// Renamed que recria o arquivo (T032).
/// </summary>
public sealed class FileWatcherService : IDisposable
{
    private const int ChangeDebounceMilliseconds = 350;
    private const int UnavailabilityDebounceMilliseconds = 100;

    private readonly string _filePath;
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _debounceTimer;
    private bool _disposed;

    public event EventHandler? DocumentChanged;
    public event EventHandler? DocumentUnavailable;

    public FileWatcherService(string filePath)
    {
        _filePath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(_filePath) ?? ".";

        _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        _watcher = new FileSystemWatcher(directory, Path.GetFileName(_filePath))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        _watcher.Changed += (_, _) => ScheduleCheck(ChangeDebounceMilliseconds);
        _watcher.Created += (_, _) => ScheduleCheck(ChangeDebounceMilliseconds);
        _watcher.Error += (_, _) => ScheduleCheck(ChangeDebounceMilliseconds);
        _watcher.Deleted += (_, _) => ScheduleCheck(UnavailabilityDebounceMilliseconds);
        _watcher.Renamed += (_, e) => OnRenamed(e);
        _watcher.EnableRaisingEvents = true;
    }

    private void OnRenamed(RenamedEventArgs e)
    {
        // Alguns editores salvam via write-temp + rename-sobre-o-original: o arquivo com o nome
        // observado continua existindo depois do rename, então tratamos como uma mudança normal.
        var isOurFile = string.Equals(Path.GetFullPath(e.FullPath), _filePath, StringComparison.OrdinalIgnoreCase);
        ScheduleCheck(isOurFile ? ChangeDebounceMilliseconds : UnavailabilityDebounceMilliseconds);
    }

    private void ScheduleCheck(int delayMilliseconds)
    {
        // Reinicia o timer a cada evento bruto: só dispara depois de delayMilliseconds de silêncio.
        _debounceTimer.Change(delayMilliseconds, Timeout.Infinite);
    }

    private void OnDebounceElapsed(object? state)
    {
        if (_disposed)
        {
            return;
        }

        if (File.Exists(_filePath))
        {
            DocumentChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            DocumentUnavailable?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _debounceTimer.Dispose();
    }
}
