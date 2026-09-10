using System;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Utils;

public class ThrottleLastDispatcher(TimeSpan interval, string? tag = null)
{
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly object _sync = new();

    public async Task DispatchAsync(Func<Task> task)
    {
        CancellationTokenSource cts;
        lock (_sync)
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
            }
            catch (ObjectDisposedException) { }

            _cancellationTokenSource = new CancellationTokenSource();
            cts = _cancellationTokenSource;
        }

        try
        {
            await Task.Delay(interval, cts.Token).ConfigureAwait(false);
            cts.Token.ThrowIfCancellationRequested();

            if (tag is not null)
                Log.Instance.Trace($"Allowing... [tag={tag}]");

            await task().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (tag is not null)
                Log.Instance.Trace($"Throttling... [tag={tag}]");
        }
    }

    public async Task DispatchImmediateAsync(Func<Task> task)
    {
        lock (_sync)
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
            }
            catch (ObjectDisposedException) { }

            _cancellationTokenSource = null;
        }

        if (tag is not null)
            Log.Instance.Trace($"Immediate dispatch... [tag={tag}]");

        await task().ConfigureAwait(false);
    }
}
