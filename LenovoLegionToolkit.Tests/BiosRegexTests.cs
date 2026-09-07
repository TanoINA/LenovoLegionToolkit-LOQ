using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LenovoLegionToolkit.Tests;

[TestClass]
public class BiosRegexTests
{
    [TestMethod]
    [DataRow("R3CN44WW", "R3CN", 44)]
    [DataRow("GKCN50WW", "GKCN", 50)]
    [DataRow("EFCN38WW", "EFCN", 38)]
    [DataRow("LYCN36WW", "LYCN", 36)]
    public void BiosVersionRegex_StandardTwoDigitVersions_ParsedCorrectly(string raw, string expectedPrefix, int expectedVersion)
    {
        var (biosVersion, rawResult) = Compatibility.ParseBiosVersionString(raw);

        Assert.IsNotNull(biosVersion);
        Assert.AreEqual(expectedPrefix, biosVersion.Value.Prefix);
        Assert.AreEqual(expectedVersion, biosVersion.Value.Version);
        Assert.AreEqual(raw, rawResult);
    }

    [TestMethod]
    [DataRow("EFCN101WW", "EFCN", 101)]
    [DataRow("H1CN105WW", "H1CN", 105)]
    [DataRow("ABCD999WW", "ABCD", 999)]
    public void BiosVersionRegex_ThreeDigitVersions_ParsedWithoutTruncation(string raw, string expectedPrefix, int expectedVersion)
    {
        var (biosVersion, rawResult) = Compatibility.ParseBiosVersionString(raw);

        Assert.IsNotNull(biosVersion);
        Assert.AreEqual(expectedPrefix, biosVersion.Value.Prefix);
        Assert.AreEqual(expectedVersion, biosVersion.Value.Version);
        Assert.AreEqual(raw, rawResult);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("INVALID")]
    [DataRow("NO_NUMBERS")]
    public void BiosVersionRegex_InvalidInputs_ReturnsNull(string raw)
    {
        var (biosVersion, rawResult) = Compatibility.ParseBiosVersionString(raw);

        Assert.IsNull(biosVersion);
    }

    [TestMethod]
    public void BiosVersion_ComparisonMethods_WorkCorrectly()
    {
        var v44 = new BiosVersion("R3CN", 44);
        var v45 = new BiosVersion("R3CN", 45);
        var v100 = new BiosVersion("R3CN", 100);

        Assert.IsTrue(v45.IsHigherOrEqualThan(v44));
        Assert.IsTrue(v44.IsHigherOrEqualThan(v44));
        Assert.IsFalse(v44.IsHigherOrEqualThan(v45));

        Assert.IsTrue(v44.IsLowerThan(v45));
        Assert.IsFalse(v45.IsLowerThan(v44));

        Assert.IsTrue(v100.IsHigherOrEqualThan(v45));
        Assert.IsTrue(v45.IsLowerThan(v100));
    }
}
