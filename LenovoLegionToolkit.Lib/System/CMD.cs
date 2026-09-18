using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.System;

public static class CMD
{
    public static async Task<(int, string)> RunAsync(string file, string arguments, bool createNoWindow = true, bool waitForExit = true, Dictionary<string, string?>? environment = null, CancellationToken token = default)
    {
        Log.Instance.Trace($"Running... [file={file}, argument={arguments}, createNoWindow={createNoWindow}, waitForExit={waitForExit}, environment=[{(environment is null ? string.Empty : string.Join(",", environment))}]");

        using var cmd = new Process();
        cmd.StartInfo.UseShellExecute = false;
        cmd.StartInfo.CreateNoWindow = createNoWindow;
        cmd.StartInfo.RedirectStandardOutput = createNoWindow && waitForExit;
        cmd.StartInfo.RedirectStandardError = createNoWindow && waitForExit;
        cmd.StartInfo.WindowStyle = createNoWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal;
        cmd.StartInfo.FileName = file;
        if (!string.IsNullOrWhiteSpace(arguments))
            cmd.StartInfo.Arguments = arguments;

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
                cmd.StartInfo.Environment[key] = value;
        }

        token.ThrowIfCancellationRequested();
        cmd.Start();

        if (!waitForExit)
        {
            Log.Instance.Trace($"Ran [file={file}, argument={arguments}, createNoWindow={createNoWindow}, waitForExit={waitForExit}, environment=[{(environment is null ? string.Empty : string.Join(",", environment))}]");

            return (-1, string.Empty);
        }

        var outputTask = createNoWindow ? cmd.StandardOutput.ReadToEndAsync(token) : Task.FromResult(string.Empty);
        var errorTask = createNoWindow ? cmd.StandardError.ReadToEndAsync(token) : Task.FromResult(string.Empty);
        await Task.WhenAll(cmd.WaitForExitAsync(token), outputTask, errorTask).ConfigureAwait(false);

        var exitCode = cmd.ExitCode;
        var output = await outputTask.ConfigureAwait(false);

        Log.Instance.Trace($"Ran [file={file}, argument={arguments}, createNoWindow={createNoWindow}, waitForExit={waitForExit}, exitCode={exitCode} output={output}]");

        return (exitCode, output);
    }
}
