using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Autopatch.Server.Models;
using Autopatch.Server.Services;
using Autopatch.Server.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Autopatch.Server.Tests;

[TestClass]
public sealed class BulkFlushQueueTests
{
    private static BulkFlushQueue<string> CreateQueue(
        CapturingLoggerFactory? logs = null,
        int maxBatchSize = 1_000,
        TimeSpan? throttleInterval = null)
        => new(
            Options.Create(new AutopatchOptions()),
            Options.Create(new ObjectTypeConfiguration<string>
            {
                MaxBatchSize = maxBatchSize,
                ThrottleInterval = throttleInterval ?? TimeSpan.FromHours(1),
            }),
            (logs ?? new CapturingLoggerFactory()).CreateLogger<BulkFlushQueue<string>>());

    [TestMethod]
    [TestCategory("S11")]
    public async Task S11_Add_DoesNotWaitForASlowSend()
    {
        var sendInProgress = new TaskCompletionSource();
        using var queue = CreateQueue(maxBatchSize: 1);
        queue.OnFlush += _ => sendInProgress.Task;

        var add = queue.Add("first");
        var completedInTime = await Task.WhenAny(add, Task.Delay(TimeSpan.FromSeconds(1))) == add;
        sendInProgress.SetResult();
        await add;

        completedInTime.Should().BeTrue(
            "reaching MaxBatchSize flushes inside Add while holding the semaphore, so the producer waits for the whole SignalR send");
    }

    [TestMethod]
    [TestCategory("S11")]
    [DoNotParallelize]
    public async Task S11_PropertyChanges_DoNotQueueOneThreadPoolWorkItemEach()
    {
        using var harness = new TrackerHarness<TestItem>();
        var item = TestItem.Create(1);
        harness.Collection.Add(item);
        await harness.SettleAsync(1);

        // Other tests leave timers behind, so subtract the background rate of work items.
        var idle = Stopwatch.StartNew();
        var idleStart = ThreadPool.CompletedWorkItemCount;
        await Task.Delay(300);
        var backgroundPerMs = (ThreadPool.CompletedWorkItemCount - idleStart) / idle.Elapsed.TotalMilliseconds;

        const int changes = 10_000;
        var measured = Stopwatch.StartNew();
        var start = ThreadPool.CompletedWorkItemCount;
        for (var value = 1; value <= changes; value++)
        {
            item.Value = value;
        }
        await harness.SettleAsync(changes + 1);
        var workItems = ThreadPool.CompletedWorkItemCount - start - (long)(backgroundPerMs * measured.Elapsed.TotalMilliseconds);

        workItems.Should().BeLessThan(changes / 10,
            "every change is enqueued via its own Task.Run, which piles up an unbounded number of tasks");
    }

    [TestMethod]
    [TestCategory("S12")]
    public void S12_NoAsyncVoidMethods()
    {
        var asyncVoidMethods = typeof(BulkFlushQueue<>)
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Concat(typeof(BulkFlushQueue<>).GetNestedTypes(BindingFlags.NonPublic)
                .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)))
            .Where(m => m.ReturnType == typeof(void) && m.GetCustomAttribute<AsyncStateMachineAttribute>() is not null)
            .Select(m => m.Name);

        asyncVoidMethods.Should().BeEmpty(
            "the Timer callback 'async _ => await TimerCallback()' is async void: an exception there (e.g. ObjectDisposedException after Dispose) terminates the process");
    }

    [TestMethod]
    [TestCategory("S12")]
    public async Task S12_DisposeDuringAFlush_DoesNotThrow()
    {
        var sendInProgress = new TaskCompletionSource();
        var queue = CreateQueue(maxBatchSize: 1);
        queue.OnFlush += _ => sendInProgress.Task;

        var add = queue.Add("first");
        await Task.Delay(100);
        queue.Dispose();
        sendInProgress.SetResult();

        var act = async () => await add;
        await act.Should().NotThrowAsync(
            "the semaphore is disposed while the flush still holds it, so Release() throws ObjectDisposedException - in the timer path this happens inside async void");
    }

    [TestMethod]
    [TestCategory("S13")]
    public async Task S13_RegularFlush_DoesNotLogAtInformationLevel()
    {
        var logs = new CapturingLoggerFactory();
        using var queue = CreateQueue(logs);
        queue.OnFlush += _ => Task.CompletedTask;

        await queue.Add("item");
        await queue.Flush();

        logs.Entries.Where(e => e.Level >= LogLevel.Information).Select(e => e.Message)
            .Should().BeEmpty("every flush is logged at Information, which floods the log at game-tick rate");
    }

    [TestMethod]
    [TestCategory("S14")]
    [DoNotParallelize]
    public async Task S14_IdleQueue_DoesNotKeepATimerRunning()
    {
        var timersBefore = Timer.ActiveCount;
        using var queue = CreateQueue(throttleInterval: TimeSpan.FromMilliseconds(20));
        queue.OnFlush += _ => Task.CompletedTask;

        await queue.Add("item");
        await Task.Delay(300);

        (Timer.ActiveCount - timersBefore).Should().Be(0,
            "after the first Add the periodic timer keeps firing every ThrottleInterval, even when nothing is queued");
    }
}
