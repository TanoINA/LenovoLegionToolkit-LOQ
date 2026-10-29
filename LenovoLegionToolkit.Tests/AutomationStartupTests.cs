using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation;
using LenovoLegionToolkit.Lib.Automation.Pipeline;
using LenovoLegionToolkit.Lib.Automation.Steps;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LenovoLegionToolkit.Tests;

[TestClass]
public class AutomationStartupTests
{
    [TestMethod]
    public async Task StartupRun_SkipsDangerousStepWithoutStartupTrigger()
    {
        var step = new CountingStep { IsDangerousOnStartup = true };
        var pipeline = new AutomationPipeline { Steps = [step] };

        await RunPipelineAsync(pipeline, isStartup: true);

        Assert.AreEqual(0, step.RunCount);
    }

    [TestMethod]
    public async Task ManualRun_ExecutesDangerousStep()
    {
        var step = new CountingStep { IsDangerousOnStartup = true };
        var pipeline = new AutomationPipeline { Steps = [step] };

        await RunPipelineAsync(pipeline, isStartup: false);

        Assert.AreEqual(1, step.RunCount);
    }

    private static async Task RunPipelineAsync(AutomationPipeline pipeline, bool isStartup)
    {
        var method = typeof(AutomationPipeline).GetMethod("RunAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                     ?? throw new MissingMethodException(typeof(AutomationPipeline).FullName, "RunAsync");
        var task = (Task)(method.Invoke(pipeline, [new List<AutomationPipeline>(), CancellationToken.None, isStartup])
                          ?? throw new InvalidOperationException("Pipeline invocation returned null."));
        await task;
    }

    private sealed class CountingStep : IAutomationStep
    {
        public bool IsDangerousOnStartup { get; init; }
        public int RunCount { get; private set; }

        public Task<bool> IsSupportedAsync() => Task.FromResult(true);

        public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
        {
            RunCount++;
            return Task.CompletedTask;
        }

        public IAutomationStep DeepCopy() => this;
    }
}
