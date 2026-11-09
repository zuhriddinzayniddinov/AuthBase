using System.Collections.Concurrent;
using Core.Domain.Permissions;

namespace Core.Web.Constants;

/// <summary>
/// Web qatlamidagi umumiy kesh proxy klassi.
/// Barcha murojaatlar <see cref="CoreStaticCache"/> ga yo'naltiriladi.
/// </summary>
public static class StaticCache
{
    public static ConcurrentDictionary<long, List<int>> Permissions
    {
        get => CoreStaticCache.Permissions;
        set => CoreStaticCache.Permissions = value;
    }

    public static string SymmetricKey
    {
        get => CoreStaticCache.SymmetricKey;
        set => CoreStaticCache.SymmetricKey = value;
    }
}
