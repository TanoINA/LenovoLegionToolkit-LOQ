using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LenovoLegionToolkit.Tests;

[TestClass]
public class HttpDownloadTests
{
    [TestMethod]
    public async Task DownloadAsync_HttpError_ThrowsInsteadOfReturningErrorPayload()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.NotFound, "update-not-found"));
        await using var destination = new MemoryStream();

        var exception = await Assert.ThrowsExceptionAsync<HttpRequestException>(() =>
            client.DownloadAsync("https://updates.invalid/setup.exe", destination));

        Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
    }

    [TestMethod]
    public async Task DownloadAsync_Success_PreservesPayload()
    {
        const string payload = "valid-package-payload";
        using var client = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, payload));
        await using var destination = new MemoryStream();

        await client.DownloadAsync("https://updates.invalid/package.exe", destination);

        Assert.AreEqual(payload, System.Text.Encoding.UTF8.GetString(destination.ToArray()));
    }

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
    }
}
