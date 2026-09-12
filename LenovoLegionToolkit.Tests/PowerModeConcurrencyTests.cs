using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LenovoLegionToolkit.Tests;

[TestClass]
public class PowerModeConcurrencyTests
{
    private class SimulatedPowerModeController
    {
        private readonly SemaphoreSlim _lock = new(1, 1);
        private int _concurrentExecutions = 0;
        private int _maxConcurrentExecutions = 0;
        public int CompletedTransitions { get; private set; } = 0;
        public List<int> ModeHistory { get; } = [];

        public async Task ChangeModeAsync(int targetMode, int workDurationMs = 20, bool throwException = false)
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var current = Interlocked.Increment(ref _concurrentExecutions);
                lock (ModeHistory)
                {
                    if (current > _maxConcurrentExecutions)
                        _maxConcurrentExecutions = current;
                }

                if (throwException)
                {
                    throw new InvalidOperationException("Simulated failure in mode transition");
                }

                // Simulate asynchronous work (e.g., GodMode apply, Power Plan update)
                await Task.Delay(workDurationMs).ConfigureAwait(false);

                lock (ModeHistory)
                {
                    ModeHistory.Add(targetMode);
                    CompletedTransitions++;
                }
            }
            finally
            {
                Interlocked.Decrement(ref _concurrentExecutions);
                _lock.Release();
            }
        }

        public int GetMaxConcurrentExecutions() => _maxConcurrentExecutions;
    }

    [TestMethod]
    public async Task Concurrency_MultipleParallelModeSwitches_SerializedStrictlyToOne()
    {
        var controller = new SimulatedPowerModeController();
        const int taskCount = 10;
        var tasks = new List<Task>();

        // Launch 10 concurrent requests to switch modes rapidly
        for (int i = 0; i < taskCount; i++)
        {
            var mode = i % 4;
            tasks.Add(Task.Run(() => controller.ChangeModeAsync(mode, workDurationMs: 15)));
        }

        await Task.WhenAll(tasks);

        Assert.AreEqual(taskCount, controller.CompletedTransitions);
        Assert.AreEqual(1, controller.GetMaxConcurrentExecutions(), "Max concurrent executions must never exceed 1 (strict serialization).");
        Assert.AreEqual(taskCount, controller.ModeHistory.Count);
    }

    [TestMethod]
    public async Task Concurrency_ExceptionInsideCriticalSection_ReleasesLockWithoutDeadlock()
    {
        var controller = new SimulatedPowerModeController();

        // Call with exception
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            controller.ChangeModeAsync(targetMode: 1, workDurationMs: 10, throwException: true));

        // Ensure subsequent call succeeds immediately (proving finally { lock.Release() } executed)
        var postCrashTask = controller.ChangeModeAsync(targetMode: 2, workDurationMs: 10, throwException: false);
        var completed = await Task.WhenAny(postCrashTask, Task.Delay(2000));

        Assert.AreEqual(postCrashTask, completed, "Subsequent call must acquire lock without deadlocking after a prior exception.");
        Assert.AreEqual(1, controller.CompletedTransitions);
    }

    [TestMethod]
    public async Task ThrottleLastDispatcher_RapidDispatches_ExecutesOnlyTrailingTask()
    {
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(50), "TestDispatcher");
        var executionCount = 0;
        var lastExecutedValue = -1;

        var tasks = new List<Task>();
        for (var i = 0; i < 50; i++)
        {
            var value = i;
            tasks.Add(dispatcher.DispatchAsync(() =>
            {
                Interlocked.Increment(ref executionCount);
                lastExecutedValue = value;
                return Task.CompletedTask;
            }));
            await Task.Delay(2);
        }

        await Task.WhenAll(tasks);
        await Task.Delay(100);

        Assert.AreEqual(49, lastExecutedValue, "Only the last dispatched work must execute.");
        Assert.IsTrue(executionCount <= 2, $"Execution count should be throttled (expected <= 2, got {executionCount})");
    }

    [TestMethod]
    public async Task ThrottleLastDispatcher_CallbackException_IsNotSwallowed()
    {
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(20), "TestDispatcher");

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
        {
            await dispatcher.DispatchAsync(() => throw new InvalidOperationException("Callback failed"));
        });
    }

    [TestMethod]
    public async Task ThrottleLastDispatcher_ImmediateDispatch_OverridesPendingDelayed()
    {
        var dispatcher = new ThrottleLastDispatcher(TimeSpan.FromMilliseconds(100), "TestDispatcher");
        var executed = new List<string>();

        _ = dispatcher.DispatchAsync(() =>
        {
            executed.Add("delayed");
            return Task.CompletedTask;
        });

        await Task.Delay(10);

        await dispatcher.DispatchImmediateAsync(() =>
        {
            executed.Add("immediate");
            return Task.CompletedTask;
        });

        await Task.Delay(150);

        CollectionAssert.AreEqual(new[] { "immediate" }, executed, "Immediate dispatch must supersede pending delayed dispatch.");
    }
}
