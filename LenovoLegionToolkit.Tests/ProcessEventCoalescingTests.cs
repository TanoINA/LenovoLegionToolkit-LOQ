using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LenovoLegionToolkit.Tests;

/// <summary>
/// Regression tests for the <c>ProcessEvent</c> event-coalescing pattern
/// introduced in v2.35.4.6 (hotfix pass 2). Verifies that bursts of events do
/// not grow unbounded concurrent tasks and that the most recent event arriving
/// during an in-flight cycle is actually processed (not silently dropped).
/// </summary>
[TestClass]
public class ProcessEventCoalescingTests
{
    private class SimulatedEventProcessor
    {
        private int _processingActive;
        private int _pendingEvent;
        private int _maxConcurrent;
        private int _currentConcurrent;

        public int ProcessedCount { get; private set; }
        public List<int> ProcessedEvents { get; } = [];
        public int MaxConcurrent => _maxConcurrent;

        public async Task ProcessEvent(int eventId, int workDurationMs = 15)
        {
            // Mirror of AutomationProcessor.ProcessEvent: latest-wins swap + drain loop.
            _pendingEvent = eventId;
            if (Interlocked.CompareExchange(ref _processingActive, 1, 0) != 0)
                return;

            try
            {
                int current;
                while ((current = Interlocked.Exchange(ref _pendingEvent, -1)) != -1)
                {
                    try
                    {
                        var c = Interlocked.Increment(ref _currentConcurrent);
                        lock (ProcessedEvents)
                        {
                            if (c > _maxConcurrent)
                                _maxConcurrent = c;
                        }

                        await Task.Delay(workDurationMs).ConfigureAwait(false);

                        lock (ProcessedEvents)
                        {
                            ProcessedEvents.Add(current);
                            ProcessedCount++;
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _currentConcurrent);
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _processingActive, 0);
            }
        }
    }

    [TestMethod]
    public async Task ProcessEvent_BurstOfEvents_ProcessesAtLeastOneAndNeverExceedsOneConcurrent()
    {
        var processor = new SimulatedEventProcessor();

        // Fire a burst of overlapping events; the coalescing must cap concurrency at 1.
        var tasks = new List<Task>();
        for (int i = 0; i < 20; i++)
        {
            var id = i;
            tasks.Add(Task.Run(() => processor.ProcessEvent(id, workDurationMs: 10)));
        }

        var allDone = Task.WhenAll(tasks);
        var done = await Task.WhenAny(allDone, Task.Delay(5000));
        Assert.AreEqual(allDone, done, "All ProcessEvent calls should return promptly; a deadlock means the guard never released.");

        // Allow a trailing drain to settle.
        await Task.Delay(100);

        Assert.IsTrue(processor.ProcessedCount >= 1, "At least one event must be processed.");
        Assert.AreEqual(1, processor.MaxConcurrent, "Concurrent processing must never exceed 1.");
    }

    [TestMethod]
    public async Task ProcessEvent_EventArrivingMidCycle_IsNotDropped()
    {
        var processor = new SimulatedEventProcessor();

        // Start a long-ish cycle (event 1).
        var first = Task.Run(() => processor.ProcessEvent(1, workDurationMs: 60));

        // While it is in-flight, submit event 2 — it must be captured and later processed.
        await Task.Delay(15);
        var second = Task.Run(() => processor.ProcessEvent(2, workDurationMs: 10));

        await Task.WhenAll(first, second);
        await Task.Delay(50);

        Assert.IsTrue(processor.ProcessedEvents.Contains(2),
            "Event 2 arrived mid-cycle and must be processed (latest-wins), not dropped.");
    }

    [TestMethod]
    public async Task ProcessEvent_NoConcurrentProcessingAcrossOverlappingCalls()
    {
        var processor = new SimulatedEventProcessor();

        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            var id = i;
            tasks.Add(Task.Run(() => processor.ProcessEvent(id, workDurationMs: 5)));
        }

        await Task.WhenAll(tasks);
        await Task.Delay(80);

        Assert.AreEqual(1, processor.MaxConcurrent,
            "Strict serialization must hold even under high contention.");
        Assert.IsTrue(processor.ProcessedCount >= 1);
    }
}
