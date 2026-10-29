using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

[method: JsonConstructor]
public class RunAutomationStep(string? scriptPath, string? scriptArguments, bool? runSilently, bool? waitUntilFinished, bool? checkInstance)
    : IAutomationStep
{
    public string? ScriptPath { get; } = scriptPath;

    public string? ScriptArguments { get; } = scriptArguments;

    public bool RunSilently { get; } = runSilently ?? true;

    public bool WaitUntilFinished { get; } = waitUntilFinished ?? false;

    public bool CheckInstance { get; } = checkInstance ?? false;

    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public async Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(ScriptPath))
            return;

        if (CheckInstance)
        {
            var executableName = Path.GetFileNameWithoutExtension(ScriptPath);
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (string.Equals(process.ProcessName, executableName, StringComparison.OrdinalIgnoreCase))
                            return;
                    }
                    catch { /* Ignore processes that exited or cannot be inspected. */ }
                }
            }
        }

        var (_, output) = await CMD.RunAsync(ScriptPath,
            ScriptArguments ?? string.Empty,
            RunSilently,
            WaitUntilFinished,
            environment.Dictionary,
            token).ConfigureAwait(false);
        context.LastRunOutput = output.TrimEnd();
    }

    IAutomationStep IAutomationStep.DeepCopy() => new RunAutomationStep(ScriptPath, ScriptArguments, RunSilently, WaitUntilFinished, CheckInstance);
}
