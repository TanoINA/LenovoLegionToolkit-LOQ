using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LenovoLegionToolkit.Tests;

/// <summary>
/// Regression tests for the ITS mode <c>_setStateLock</c> pattern introduced in v2.35.4.6.
/// Mirrors the structure of <see cref="ITSModeFeature.SetStateAsync"/>: a dedicated
/// <see cref="SemaphoreSlim"/> acquired with <c>WaitAsync</c> and released in a
/// <c>try/finally</c> block so that <see cref="InvalidOperationException"/> thrown
/// mid-transition (unsupported state, or mode-did-not-change) cannot permanently hold
/// the lock and deadlock subsequent Fn+Q presses.
/// </summary>
[TestClass]
public class SetStateLockReleaseTests
{
    private class SimulatedITSModeSetter
    {
        private readonly SemaphoreSlim _setStateLock = new(1, 1);
        private int _concurrentExecutions = 0;
        private int _maxConcurrentExecutions = 0;
        public int CompletedTransitions { get; private set; } = 0;
        public List<int> ModeHistory { get; } = [];

        public async Task SetStateAsync(int state, bool throwUnsupported = false, bool throwNotChanged = false, int workDurationMs = 20)
        {
            await _setStateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var current = Interlocked.Increment(ref _concurrentExecutions);
                lock (ModeHistory)
                {
                    if (current > _maxConcurrentExecutions)
                        _maxConcurrentExecutions = current;
                }

                if (throwUnsupported)
                    throw new InvalidOperationException($"Unsupported ITS mode {state}.");

                await Task.Delay(workDurationMs).ConfigureAwait(false);

                if (throwNotChanged)
                    throw new InvalidOperationException($"ITS mode did not change to {state}.");

                lock (ModeHistory)
                {
                    ModeHistory.Add(state);
                    CompletedTransitions++;
                }
            }
            finally
            {
                Interlocked.Decrement(ref _concurrentExecutions);
                _setStateLock.Release();
            }
        }

        public int GetMaxConcurrentExecutions() => _maxConcurrentExecutions;

        public bool IsLockHeld()
        {
            var got = _setStateLock.Wait(0);
            if (got) _setStateLock.Release();
            return !got;
        }
    }

    [TestMethod]
    public async Task SetState_UnsupportedState_Throws_AndReleasesLockWithoutDeadlock()
    {
        var setter = new SimulatedITSModeSetter();

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            setter.SetStateAsync(state: 99, throwUnsupported: true));

        Assert.IsFalse(setter.IsLockHeld(), "Lock must be released after unsupported-state exception.");

        var postCrashTask = setter.SetStateAsync(state: 1, workDurationMs: 10);
        var completed = await Task.WhenAny(postCrashTask, Task.Delay(2000));

        Assert.AreEqual(postCrashTask, completed, "Subsequent SetState must acquire lock without deadlocking after a prior unsupported-state exception.");
        Assert.AreEqual(1, setter.CompletedTransitions);
    }

    [TestMethod]
    public async Task SetState_ModeDidNotChange_Throws_AndReleasesLockWithoutDeadlock()
    {
        var setter = new SimulatedITSModeSetter();

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            setter.SetStateAsync(state: 1, throwNotChanged: true));

        Assert.IsFalse(setter.IsLockHeld(), "Lock must be released after mode-did-not-change exception.");

        var postCrashTask = setter.SetStateAsync(state: 2, workDurationMs: 10);
        var completed = await Task.WhenAny(postCrashTask, Task.Delay(2000));

        Assert.AreEqual(postCrashTask, completed, "Subsequent SetState must acquire lock without deadlocking after a prior mode-did-not-change exception.");
        Assert.AreEqual(1, setter.CompletedTransitions);
    }

    [TestMethod]
    public async Task SetState_MultipleParallelRequests_SerializedStrictlyToOne()
    {
        var setter = new SimulatedITSModeSetter();
        const int taskCount = 12;
        var tasks = new List<Task>();

        for (int i = 0; i < taskCount; i++)
        {
            var mode = i % 4;
            tasks.Add(Task.Run(() => setter.SetStateAsync(mode, workDurationMs: 15)));
        }

        await Task.WhenAll(tasks);

        Assert.AreEqual(taskCount, setter.CompletedTransitions);
        Assert.AreEqual(1, setter.GetMaxConcurrentExecutions(), "Max concurrent executions must never exceed 1 (strict serialization).");
        Assert.AreEqual(taskCount, setter.ModeHistory.Count);
    }

    [TestMethod]
    public async Task SetState_ExceptionThenSuccessThenException_LockAlwaysReleased()
    {
        var setter = new SimulatedITSModeSetter();

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            setter.SetStateAsync(state: 1, throwNotChanged: true));
        Assert.IsFalse(setter.IsLockHeld());

        await setter.SetStateAsync(state: 2, workDurationMs: 5);
        Assert.AreEqual(1, setter.CompletedTransitions);
        Assert.IsFalse(setter.IsLockHeld());

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            setter.SetStateAsync(state: 99, throwUnsupported: true));
        Assert.IsFalse(setter.IsLockHeld());

        await setter.SetStateAsync(state: 3, workDurationMs: 5);
        Assert.AreEqual(2, setter.CompletedTransitions);
    }
}
