using System.Collections.Generic;

namespace LenovoLegionToolkit.Lib.SoftwareDisabler;

public class LegionSpaceDisabler : AbstractSoftwareDisabler
{
    protected override IEnumerable<string> ScheduledTasksPaths => [];
    protected override IEnumerable<string> ServiceNames =>
    [
        "DAService",
        "LenovoSmartService",
        "GAService"
    ];

    protected override IEnumerable<string> ProcessNames =>
    [
        "LegionSpace",
        "LSDaemon",
        "SmartEngineHost64",
        "SmartEngineHostN64",
        "SmartEngineHostS64",
        "LenovoSmartService",
        "GAService",
        "GAController",
        "GAWorker",
        "seworker"
    ];
}
