using System.Collections.Generic;
using System.Linq;

namespace LenovoLegionToolkit.Lib.Settings;

public class AmdOverclockingSettings() : AbstractSettings<AmdOverclockingSettings.AmdOverclockingSettingsStore>("amd_oc.json")
{
    private readonly object _profileLock = new();

    public class AmdOverclockingSettingsStore
    {
        public bool Enabled { get; set; }
        public bool AllowOnBattery { get; set; }
        public bool AllowInAllPowerModes { get; set; }
        public uint? FMax { get; set; }
        public List<double?> CoreValues { get; set; } = [];
        public short? PowerLimit1 { get; set; }
        public short? PowerLimit2 { get; set; }
        public short? PowerLimit3 { get; set; }
        public short? EDCSoc { get; set; }
        public short? EDCVdd { get; set; }
        public short? TDCSoc { get; set; }
        public short? TDCVdd { get; set; }
        public Dictionary<CurveShapeLevel, List<int>>? CurveShapeValues { get; set; } = [];
    }

    public bool HasProfile
    {
        get
        {
            lock (_profileLock)
            {
                return Store.FMax.HasValue ||
                       (Store.CoreValues?.Any(v => v.HasValue) == true) ||
                       Store.PowerLimit1.HasValue ||
                       Store.PowerLimit2.HasValue ||
                       Store.PowerLimit3.HasValue ||
                       Store.EDCSoc.HasValue ||
                       Store.EDCVdd.HasValue ||
                       Store.TDCSoc.HasValue ||
                       Store.TDCVdd.HasValue ||
                       (Store.CurveShapeValues?.Any(kvp => kvp.Value.Any(v => v != 0)) == true);
            }
        }
    }

    public OverclockingProfile? GetProfile()
    {
        lock (_profileLock)
        {
            if (!HasProfile)
                return null;

            return new OverclockingProfile
            {
                FMax = Store.FMax,
                CoreValues = Store.CoreValues?.ToList() ?? [],
                PowerLimit1 = Store.PowerLimit1,
                PowerLimit2 = Store.PowerLimit2,
                PowerLimit3 = Store.PowerLimit3,
                EDCSoc = Store.EDCSoc,
                EDCVdd = Store.EDCVdd,
                TDCSoc = Store.TDCSoc,
                TDCVdd = Store.TDCVdd,
                CurveShapeValues = CopyNonZeroCurveValues(Store.CurveShapeValues)
            };
        }
    }

    public void SetProfile(OverclockingProfile profile)
    {
        lock (_profileLock)
        {
            Store.FMax = profile.FMax;
            Store.CoreValues = profile.CoreValues?.ToList() ?? [];
            Store.PowerLimit1 = profile.PowerLimit1;
            Store.PowerLimit2 = profile.PowerLimit2;
            Store.PowerLimit3 = profile.PowerLimit3;
            Store.EDCSoc = profile.EDCSoc;
            Store.EDCVdd = profile.EDCVdd;
            Store.TDCSoc = profile.TDCSoc;
            Store.TDCVdd = profile.TDCVdd;
            Store.CurveShapeValues = CopyNonZeroCurveValues(profile.CurveShapeValues);
            SynchronizeStore();
        }
    }

    private static Dictionary<CurveShapeLevel, List<int>>? CopyNonZeroCurveValues(Dictionary<CurveShapeLevel, List<int>>? values) =>
        values?.Where(kvp => kvp.Value.Any(v => v != 0))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
}
