using System;
using System.IO.Pipes;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.CLI.Lib.Extensions;

public static class PipeStreamExtensions
{
    public const int MaximumMessageSize = 1024 * 1024;
    private static readonly Encoding Encoding = Encoding.UTF8;

    public static async Task WriteObjectAsync<T>(this PipeStream stream, T obj, CancellationToken token = default)
    {
        if (stream.ReadMode != PipeTransmissionMode.Message)
            throw new InvalidOperationException("ReadMode is not PipeTransmissionMode.Message");

        var str = JsonConvert.SerializeObject(obj);
        var bytes = Encoding.GetBytes(str);
        if (bytes.Length > MaximumMessageSize)
            throw new InvalidDataException("IPC message exceeds the maximum size.");
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
    }

    public static async Task<T?> ReadObjectAsync<T>(this PipeStream stream, CancellationToken token = default)
    {
        if (stream.ReadMode != PipeTransmissionMode.Message)
            throw new InvalidOperationException("ReadMode is not PipeTransmissionMode.Message");

        var buffer = new byte[1024];
        using var message = new MemoryStream();

        do
        {
            var bytesRead = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
            if (bytesRead == 0)
                throw new EndOfStreamException("Pipe closed before a complete message was received.");
            if (message.Length + bytesRead > MaximumMessageSize)
                throw new InvalidDataException("IPC message exceeds the maximum size.");
            message.Write(buffer, 0, bytesRead);
        } while (!stream.IsMessageComplete);

        return JsonConvert.DeserializeObject<T>(Encoding.GetString(message.GetBuffer(), 0, (int)message.Length));
    }
}
