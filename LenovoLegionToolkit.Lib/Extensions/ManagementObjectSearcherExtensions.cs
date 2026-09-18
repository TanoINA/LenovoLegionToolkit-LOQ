using System.Management;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Extensions;

public static class ManagementObjectSearcherExtensions
{
    // The caller owns the collection and each object yielded by its enumerator.
    public static Task<ManagementObjectCollection> GetAsync(this ManagementObjectSearcher mos) => Task.Run(mos.Get);
}
