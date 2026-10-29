using System;
using System.IO;
using System.Reflection;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SecurityException = LenovoLegionToolkit.Lib.Utils.SecurityException;

namespace LenovoLegionToolkit.Tests;

[TestClass]
public class AuthenticodeVerifierTests
{
    [TestMethod]
    public void Verify_ValidSignature_SucceedsAndTamperedPayload_IsRejected()
    {
        var sourcePath = Environment.ProcessPath ?? throw new InvalidOperationException("Current test host path is unavailable.");
        var tempPath = Path.Combine(Path.GetTempPath(), $"llt-authenticode-{Guid.NewGuid():N}.exe");

        try
        {
            File.Copy(sourcePath, tempPath, overwrite: true);
            Verify(tempPath);

            using (var stream = new FileStream(tempPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                stream.Position = 4096;
                var original = stream.ReadByte();
                Assert.AreNotEqual(-1, original);
                stream.Position = 4096;
                stream.WriteByte((byte)(original ^ 0x01));
            }

            Assert.ThrowsException<SecurityException>(() => Verify(tempPath));
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static void Verify(string filePath)
    {
        var verifierType = typeof(UpdateChecker).Assembly.GetType("LenovoLegionToolkit.Lib.Utils.AuthenticodeVerifier", throwOnError: true)!;
        var method = verifierType.GetMethod("Verify", BindingFlags.Public | BindingFlags.Static)
                     ?? throw new MissingMethodException(verifierType.FullName, "Verify");
        try
        {
            method.Invoke(null, [filePath]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}
