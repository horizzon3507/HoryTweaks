using System.Collections.Concurrent;

namespace HoryTweaks.Core.Logging;

/// <summary>
/// One console log line paired with its foreground color so color and text are always emitted together.
/// </summary>
internal readonly record struct ConsoleLogEntry(ConsoleColor Color, string Line);

/// <summary>
/// Bounded single-consumer background writer that keeps synchronous console I/O off the Unity main thread
/// and guarantees atomic color+line writes across concurrent producers.
/// </summary>
internal sealed class AsyncConsoleWriter : IDisposable
{
    internal const int DefaultCapacity = 512;

    private readonly Action<ConsoleColor, string> _writeLine;
    private readonly int _capacity;
    private readonly ConcurrentQueue<ConsoleLogEntry> _queue = new();
    private readonly AutoResetEvent _signal = new(false);
    private readonly ManualResetEventSlim _drained = new(true);
    private readonly Thread _worker;

    private int _queuedCount;
    private int _droppedCount;
    private int _disposed;

    internal AsyncConsoleWriter(Action<ConsoleColor, string> writeLine, int capacity = DefaultCapacity)
    {
        if (writeLine == null)
            throw new ArgumentNullException(nameof(writeLine));
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        _writeLine = writeLine;
        _capacity = capacity;
        _worker = new Thread(RunLoop)
        {
            Name = "HoryTweaks.ConsoleWriter",
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal
        };
        _worker.Start();
    }

    internal int DroppedCount => Volatile.Read(ref _droppedCount);

    internal bool Enqueue(ConsoleColor color, string line)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return false;

        _drained.Reset();
        _queue.Enqueue(new ConsoleLogEntry(color, line));
        int count = Interlocked.Increment(ref _queuedCount);

        while (count > _capacity && _queue.TryDequeue(out _))
        {
            count = Interlocked.Decrement(ref _queuedCount);
            Interlocked.Increment(ref _droppedCount);
        }

        try
        {
            _signal.Set();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        return true;
    }

    internal bool Flush(int timeoutMs = 1000)
    {
        if (Volatile.Read(ref _queuedCount) <= 0 && _queue.IsEmpty)
            return true;

        try
        {
            _signal.Set();
            return _drained.Wait(timeoutMs);
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            _signal.Set();
        }
        catch (ObjectDisposedException) { }

        // Give the background worker a bounded window to drain pending messages without risking shutdown hangs.
        _worker.Join(500);
        _signal.Dispose();
        _drained.Dispose();
    }

    private void RunLoop()
    {
        try
        {
            while (true)
            {
                DrainQueue();

                if (Volatile.Read(ref _disposed) != 0)
                {
                    DrainQueue();
                    break;
                }

                if (_queue.IsEmpty)
                {
                    _drained.Set();
                }

                _signal.WaitOne(100);
            }
        }
        catch
        {
            // Never allow background logging faults to crash the host process.
        }
        finally
        {
            _drained.Set();
        }
    }

    private void DrainQueue()
    {
        while (_queue.TryDequeue(out var entry))
        {
            Interlocked.Decrement(ref _queuedCount);
            EmitDroppedNoticeIfNeeded();
            SafeWrite(entry.Color, entry.Line);
        }

        EmitDroppedNoticeIfNeeded();
    }

    private void EmitDroppedNoticeIfNeeded()
    {
        int dropped = Interlocked.Exchange(ref _droppedCount, 0);
        if (dropped > 0)
        {
            SafeWrite(ConsoleColor.DarkYellow, $"[Console] Dropped {dropped} buffered log line(s) while console output was busy.");
        }
    }

    private void SafeWrite(ConsoleColor color, string line)
    {
        try
        {
            _writeLine(color, line);
        }
        catch
        {
            // Swallow console stream errors (e.g., detached console or closed handle).
        }
    }
}
