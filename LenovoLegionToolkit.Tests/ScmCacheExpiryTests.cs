using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LenovoLegionToolkit.Tests;

[TestClass]
public class ScmCacheExpiryTests
{
    private class TestSoftwareDisabler : AbstractSoftwareDisabler
    {
        public int CalculationCounter { get; private set; }

        protected override IEnumerable<string> ScheduledTasksPaths => [];
        protected override IEnumerable<string> ServiceNames => [];
        protected override IEnumerable<string> ProcessNames => [];

        public TestSoftwareDisabler()
        {
            OnRefreshed += (_, _) => CalculationCounter++;
        }
    }

    [TestMethod]
    public async Task ScmCache_WithinTTL_ReturnsCachedResultWithoutRequery()
    {
        var disabler = new TestSoftwareDisabler();

        var status1 = await disabler.GetStatusAsync();
        var initialCount = disabler.CalculationCounter;

        Assert.AreEqual(SoftwareStatus.NotFound, status1);
        Assert.AreEqual(1, initialCount);

        // Immediate subsequent calls within 5 seconds must hit cache
        var status2 = await disabler.GetStatusAsync();
        var status3 = await disabler.GetStatusAsync();

        Assert.AreEqual(status1, status2);
        Assert.AreEqual(status1, status3);
        Assert.AreEqual(initialCount, disabler.CalculationCounter, "Calculation counter should not increase within 5-second TTL cache window.");
    }

    [TestMethod]
    public async Task ScmCache_ForceRefresh_BypassesCache()
    {
        var disabler = new TestSoftwareDisabler();

        await disabler.GetStatusAsync();
        Assert.AreEqual(1, disabler.CalculationCounter);

        // Force refresh must re-execute
        await disabler.GetStatusAsync(forceRefresh: true);
        Assert.AreEqual(2, disabler.CalculationCounter, "Force refresh should bypass cache and increment calculation counter.");
    }

    [TestMethod]
    public async Task ScmCache_InvalidateCache_ClearsCacheImmediately()
    {
        var disabler = new TestSoftwareDisabler();

        await disabler.GetStatusAsync();
        Assert.AreEqual(1, disabler.CalculationCounter);

        // Invalidate cache
        disabler.InvalidateCache();

        // Next call should re-query
        await disabler.GetStatusAsync();
        Assert.AreEqual(2, disabler.CalculationCounter, "Calling InvalidateCache should force next call to re-query.");
    }
}
