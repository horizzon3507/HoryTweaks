using System.Collections.Concurrent;
using HoryTweaks.Core.Logging;
using Xunit;

namespace HoryTweaks.Tests.Logging;

public class AsyncConsoleWriterTests
{
    [Fact]
    public async Task Preserves_order_and_color_pairing_under_concurrent_writes()
    {
        var written = new ConcurrentQueue<(ConsoleColor Color, string Line)>();
        using var writer = new AsyncConsoleWriter((color, line) => written.Enqueue((color, line)), capacity: 2048);

        const int workers = 4;
        const int perWorker = 50;
        var tasks = Enumerable.Range(0, workers).Select(workerId => Task.Run(() =>
        {
            var color = (ConsoleColor)((workerId % 6) + 1);
            for (int i = 0; i < perWorker; i++)
            {
                writer.Enqueue(color, $"w{workerId}-{i}");
            }
        })).ToArray();

        await Task.WhenAll(tasks);
        Assert.True(writer.Flush(3000));

        var items = written.ToArray();
        Assert.Equal(workers * perWorker, items.Length);
        foreach (var (color, line) in items)
        {
            int workerId = int.Parse(line.Split('-')[0][1..]);
            var expectedColor = (ConsoleColor)((workerId % 6) + 1);
            Assert.Equal(expectedColor, color);
        }
    }

    [Fact]
    public void Drops_oldest_without_blocking_when_consumer_stalls()
    {
        using var gate = new ManualResetEventSlim(false);
        var received = new List<(ConsoleColor Color, string Line)>();
        using var writer = new AsyncConsoleWriter((color, line) =>
        {
            gate.Wait(2000);
            lock (received)
            {
                received.Add((color, line));
            }
        }, capacity: 4);

        for (int i = 0; i < 20; i++)
        {
            Assert.True(writer.Enqueue(ConsoleColor.White, $"msg-{i}"));
        }

        gate.Set();
        Assert.True(writer.Flush(3000));

        lock (received)
        {
            Assert.Contains(received, entry => entry.Color == ConsoleColor.DarkYellow && entry.Line.Contains("Dropped"));
            Assert.Contains(received, entry => entry.Line == "msg-19");
        }
    }

    [Fact]
    public void Continues_writing_after_stream_exception()
    {
        int calls = 0;
        var succeeded = new List<string>();
        using var writer = new AsyncConsoleWriter((_, line) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                throw new IOException("simulated transient console error");

            lock (succeeded)
            {
                succeeded.Add(line);
            }
        });

        writer.Enqueue(ConsoleColor.Red, "first-fails");
        writer.Enqueue(ConsoleColor.Green, "second-succeeds");
        Assert.True(writer.Flush(3000));

        Assert.Single(succeeded);
        Assert.Equal("second-succeeds", succeeded[0]);
    }

    [Fact]
    public void Rejects_enqueue_after_dispose()
    {
        var writer = new AsyncConsoleWriter((_, _) => { });
        writer.Dispose();

        Assert.False(writer.Enqueue(ConsoleColor.White, "after-dispose"));
    }
}
