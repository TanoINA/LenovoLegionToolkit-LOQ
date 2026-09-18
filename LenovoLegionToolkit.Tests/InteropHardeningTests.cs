using System.IO.Pipes;
using System.Text;
using LenovoLegionToolkit.CLI.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;

namespace LenovoLegionToolkit.Tests;

[TestClass]
public class InteropHardeningTests
{
    [TestMethod]
    public async Task ProcessDrainsBothOutputStreams()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var (exitCode, output) = await CMD.RunAsync(
            Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            "/d /c \"for /l %i in (1,1,3000) do @(echo stdout-line& echo stderr-line 1>&2)\"",
            token: deadline.Token);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(3000, output.Split("stdout-line").Length - 1);
        Assert.IsFalse(output.Contains("stderr-line"));
    }

    [TestMethod]
    public async Task PipePreservesUtf8AcrossReadBoundaries()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var name = Guid.NewGuid().ToString("N");
        await using var server = CreateServer(name);
        await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await Task.WhenAll(server.WaitForConnectionAsync(deadline.Token), client.ConnectAsync(deadline.Token));
        client.ReadMode = PipeTransmissionMode.Message;
        var expected = new string('a', 1022) + "€😀" + new string('b', 4096);
        var read = server.ReadObjectAsync<string>(deadline.Token);
        await client.WriteObjectAsync(expected, deadline.Token);
        Assert.AreEqual(expected, await read);
    }

    [TestMethod]
    public async Task PipeRejectsOversizedIncomingMessage()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var name = Guid.NewGuid().ToString("N");
        await using var server = CreateServer(name);
        await using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await Task.WhenAll(server.WaitForConnectionAsync(deadline.Token), client.ConnectAsync(deadline.Token));
        var read = server.ReadObjectAsync<string>(deadline.Token);
        await client.WriteAsync(Encoding.UTF8.GetBytes(new string('x', PipeStreamExtensions.MaximumMessageSize + 1)), deadline.Token);
        await Assert.ThrowsExceptionAsync<InvalidDataException>(async () => await read);
    }

    [TestMethod]
    public async Task IdlePipeWaitCanBeCancelled()
    {
        await using var server = CreateServer(Guid.NewGuid().ToString("N"));
        using var cancellation = new CancellationTokenSource();
        var wait = server.WaitForConnectionAsync(cancellation.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
            await wait.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private static NamedPipeServerStream CreateServer(string name) =>
        new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
}