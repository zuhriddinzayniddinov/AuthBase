using System.Collections.Concurrent;

namespace Core.Domain.Permissions;

/// <summary>
/// Tizim bo'ylab umumiy xotira keshi (xususan ruxsatlar va simmetrik kalit).
/// </summary>
public static class CoreStaticCache
{
    /// <summary>
    /// Har bir struktura (Role) uchun unga tegishli ruxsat kodlari lug'ati.
    /// Thread-safe concurrent dictionary.
    /// </summary>
    public static ConcurrentDictionary<long, List<int>> Permissions { get; set; } = new();

    /// <summary>
    /// ID va tokenlarni shifrlash uchun simmetrik kalit.
    /// </summary>
    public static string SymmetricKey { get; set; } = string.Empty;
}
