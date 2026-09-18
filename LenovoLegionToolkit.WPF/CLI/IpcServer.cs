using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.CLI.Lib;
using LenovoLegionToolkit.CLI.Lib.Extensions;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.CLI.Features;

namespace LenovoLegionToolkit.WPF.CLI;

public class IpcServer(
    AutomationProcessor automationProcessor,
    SpectrumKeyboardBacklightController spectrumKeyboardBacklightController,
    RGBKeyboardBacklightController rgbKeyboardBacklightController,
    IntegrationsSettings settings
    )
{
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private static readonly TimeSpan TransportTimeout = TimeSpan.FromSeconds(10);
    private CancellationTokenSource? _cancellationTokenSource;
    private Task _handler = Task.CompletedTask;

    public async Task StartStopIfNeededAsync()
    {
        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
            if (!settings.Store.CLI)
                return;
            _cancellationTokenSource = new();
            var token = _cancellationTokenSource.Token;
            _handler = Task.Run(() => Handler(token));
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task StopCoreAsync()
    {
        if (_cancellationTokenSource is null)
            return;
        await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
        await _handler.ConfigureAwait(false);
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
        _handler = Task.CompletedTask;
    }

    private async Task Handler(CancellationToken token)
    {
        try
        {
            using var windowsIdentity = WindowsIdentity.GetCurrent();
            var identity = windowsIdentity.User ?? throw new InvalidOperationException("Current user SID is unavailable.");
            var security = new PipeSecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new(identity, PipeAccessRights.ReadWrite, AccessControlType.Allow));

            while (!token.IsCancellationRequested)
            {
                await using var pipe = NamedPipeServerStreamAcl.Create(LenovoLegionToolkit.CLI.Lib.Constants.PIPE_NAME,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous,
                    0,
                    0,
                    security);

                using var closeOnStop = token.Register(() => pipe.Dispose());
                try
                {
                    await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                    deadline.CancelAfter(TransportTimeout);
                    var req = await pipe.ReadObjectAsync<IpcRequest>(deadline.Token).ConfigureAwait(false);

                    if (req?.Operation is null)
                        throw new IpcException("Failed to deserialize request");

                    // Hardware/actions have their own lifetime; transport deadlines must not
                    // abandon a mutation and permit overlapping requests.
                    deadline.CancelAfter(Timeout.InfiniteTimeSpan);
                    deadline.Token.ThrowIfCancellationRequested();
                    IpcResponse res;
                    try
                    {
                        res = await HandleRequest(req).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        res = new IpcResponse { Success = false, Message = ex.Message };
                    }
                    deadline.CancelAfter(TransportTimeout);
                    await pipe.WriteObjectAsync(res, deadline.Token).ConfigureAwait(false);
                }
                catch (Exception) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Never attempt a second write on a failed transport.
                    Log.Instance.Trace($"IPC connection failed or timed out.", ex);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Instance.Trace($"Unknown failure.", ex);
        }
    }

    private async Task<IpcResponse> HandleRequest(IpcRequest req)
    {
        string? message;

        switch (req.Operation)
        {
            case IpcRequest.OperationType.ListQuickActions:
                message = await ListQuickActionsAsync().ConfigureAwait(false);
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.QuickAction when req is { Name: not null }:
                await RunQuickActionAsync(req.Name).ConfigureAwait(false);
                return new IpcResponse { Success = true };
            case IpcRequest.OperationType.ListFeatures:
                message = await ListFeaturesAsync();
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.ListFeatureValues when req is { Name: not null }:
                message = await ListFeatureValuesAsync(req.Name);
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.GetFeatureValue when req is { Name: not null }:
                message = await GetFeatureValueAsync(req.Name);
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.SetFeatureValue when req is { Name: not null, Value: not null }:
                await SetFeatureValueAsync(req.Name, req.Value).ConfigureAwait(false);
                return new IpcResponse { Success = true };
            case IpcRequest.OperationType.GetSpectrumProfile:
                message = await GetSpectrumProfileAsync();
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.SetSpectrumProfile when req is { Value: not null }:
                await SetSpectrumProfileAsync(req.Value);
                return new IpcResponse { Success = true };
            case IpcRequest.OperationType.GetSpectrumBrightness:
                message = await GetSpectrumBrightnessAsync();
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.SetSpectrumBrightness when req is { Value: not null }:
                await SetSpectrumBrightnessAsync(req.Value);
                return new IpcResponse { Success = true };
            case IpcRequest.OperationType.GetRGBPreset:
                message = await GetRGBPresetAsync();
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.SetRGBPreset when req is { Value: not null }:
                await SetRGBPresetAsync(req.Value);
                return new IpcResponse { Success = true };
            default:
                throw new IpcException("Invalid request");
        }
    }

    private async Task<string> ListQuickActionsAsync()
    {
        var pipelines = await automationProcessor.GetPipelinesAsync().ConfigureAwait(false);
        var quickActions = pipelines
            .Where(p => p.Trigger is null)
            .Select(p => p.Name);

        return string.Join('\n', quickActions);
    }

    private async Task RunQuickActionAsync(string name)
    {
        var pipelines = await automationProcessor.GetPipelinesAsync().ConfigureAwait(false);
        var quickAction = pipelines
                              .Where(p => p.Trigger is null)
                              .FirstOrDefault(p => p.Name == name)
                          ?? throw new InvalidOperationException($"Quick Action \"{name}\" not found");

        await automationProcessor.RunNowAsync(quickAction.Id);
    }

    private static async Task<string?> ListFeaturesAsync()
    {
        var features = new List<string>();

        foreach (var feature in FeatureRegistry.All)
        {
            if (await feature.IsSupportedAsync().ConfigureAwait(false))
                features.Add(feature.Name);
        }

        return string.Join('\n', features);
    }

    private static async Task<string?> ListFeatureValuesAsync(string name)
    {
        var feature = FeatureRegistry.All.FirstOrDefault(f => f.Name == name)
                      ?? throw new IpcException("Invalid feature");
        var values = await feature.GetValuesAsync().ConfigureAwait(false);
        return string.Join('\n', values);
    }

    private static async Task<string> GetFeatureValueAsync(string name)
    {
        var feature = FeatureRegistry.All.FirstOrDefault(f => f.Name == name)
                      ?? throw new IpcException("Invalid feature");
        return await feature.GetValueAsync().ConfigureAwait(false);
    }

    private static async Task SetFeatureValueAsync(string name, string value)
    {
        var feature = FeatureRegistry.All.FirstOrDefault(f => f.Name == name)
                      ?? throw new IpcException("Invalid feature");
        await feature.SetValueAsync(value).ConfigureAwait(false);
    }

    private async Task<string> GetSpectrumProfileAsync()
    {
        if (!await spectrumKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("Spectrum is not supported");

        var profile = await spectrumKeyboardBacklightController.GetProfileAsync().ConfigureAwait(false);
        return $"{profile}";
    }

    private async Task SetSpectrumProfileAsync(string value)
    {
        if (!await spectrumKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("Spectrum is not supported");

        await spectrumKeyboardBacklightController.SetProfileAsync(Convert.ToInt32(value)).ConfigureAwait(false);

        MessagingCenter.Publish(new SpectrumBacklightChangedMessage());
    }

    private async Task<string> GetSpectrumBrightnessAsync()
    {
        if (!await spectrumKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("Spectrum is not supported");

        var profile = await spectrumKeyboardBacklightController.GetBrightnessAsync().ConfigureAwait(false);
        return $"{profile}";
    }

    private async Task SetSpectrumBrightnessAsync(string value)
    {
        if (!await spectrumKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("Spectrum is not supported");

        await spectrumKeyboardBacklightController.SetBrightnessAsync(Convert.ToInt32(value)).ConfigureAwait(false);

        MessagingCenter.Publish(new SpectrumBacklightChangedMessage());
    }

    private async Task<string> GetRGBPresetAsync()
    {
        if (!await rgbKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("RGB is not supported");

        var state = await rgbKeyboardBacklightController.GetStateAsync().ConfigureAwait(false);
        return $"{(int)state.SelectedPreset + 1}";
    }

    private async Task SetRGBPresetAsync(string value)
    {
        if (!await rgbKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("RGB is not supported");

        var preset = (RGBKeyboardBacklightPreset)(Convert.ToInt32(value) - 1);

        if (!Enum.IsDefined(preset))
            throw new InvalidOperationException("Invalid preset");

        await rgbKeyboardBacklightController.SetLightControlOwnerAsync(true).ConfigureAwait(false);
        await rgbKeyboardBacklightController.SetPresetAsync(preset).ConfigureAwait(false);

        MessagingCenter.Publish(new RGBKeyboardBacklightChangedMessage());
    }
}
