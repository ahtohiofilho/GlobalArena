using System.Diagnostics;

namespace GlobalArena.Simulation;

/// <summary>
/// Cooperative, same-host interprocess checkpoint serialization. Every
/// reader/writer MUST hold the same sidecar .gate FileStream opened with
/// FileShare.None. The sidecar remains on disk: deleting a lock file on
/// disposal introduces inode/name races on Unix.
///
/// This is not a distributed lease, access-control system, fencing token,
/// OS crash durability guarantee, or protection from hostile processes.
/// </summary>
public sealed class ExperimentalCheckpointProcessGate : IDisposable
{
    private readonly FileStream _held;
    private bool _disposed;

    private ExperimentalCheckpointProcessGate(FileStream held) =>
        _held = held;

    public static ExperimentalCheckpointProcessGate Acquire(
        string checkpointPath, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointPath);
        var wait = timeout ?? TimeSpan.FromSeconds(10);
        if (wait < TimeSpan.Zero || wait > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(timeout),
                "A checkpoint gate must have a finite bounded wait.");
        var lockPath = Path.GetFullPath(checkpointPath) + ".gate";
        var clock = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new ExperimentalCheckpointProcessGate(
                    new FileStream(lockPath, FileMode.OpenOrCreate,
                        FileAccess.ReadWrite, FileShare.None, 1,
                        FileOptions.None));
            }
            catch (IOException) when (clock.Elapsed < wait)
            {
                Thread.Sleep(15);
            }
            catch (IOException e)
            {
                throw new IOException(
                    "The checkpoint is locked by another process, or its gate is unavailable.",
                    e);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _held.Dispose();
    }
}

/// <summary>
/// Controlled QA interception, only in the VS50 isolated checkpoint lab.
/// Injectors can throw (or terminate a TEST child process) at a precisely
/// defined publication boundary. Never inject arbitrary failures after
/// production payment side effects: there are none in this laboratory.
/// </summary>
public enum ExperimentalCheckpointPublicationBoundary : byte
{
    StageFlushedBeforeReplace = 1,
    FileReplacedBeforeReturn = 2
}

public interface IExperimentalCheckpointPublicationFault
{
    void OnBoundary(ExperimentalCheckpointPublicationBoundary boundary);
}
