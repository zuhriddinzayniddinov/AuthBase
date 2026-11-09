using Core.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace Core.Web.Attributes;

/// <summary>
/// Endpoint yoki Kontrollerga kirish huquqini (RBAC Permission) tekshiruvchi atribut.
/// Har qanday mikroservisning o'z lokal enumi yoki raqamli (int) kodlarini qabul qiladi:
/// 1. Har bir mikroservisning o'ziga tegishli ixtiyoriy lokal enumi (masalan: <c>QuranPermissions.CreateTrainingCenter</c>).
/// 2. Raqamli ruxsat kodlari (masalan: <c>60100001</c>).
/// </summary>
/// <example>
/// <code>
/// // Servisning o'z lokal enumi orqali:
/// [PermissionAuthorize(QuranPermissions.CreateTrainingCenter)]
/// public async Task&lt;IActionResult&gt; Create() { ... }
/// 
/// // Bir nechta ruxsatlarni tekshirish:
/// [PermissionAuthorize(QuranPermissions.CreateTrainingCenter, QuranPermissions.UpdateTrainingCenter)]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class PermissionAuthorizeAttribute : TypeFilterAttribute
{
    /// <summary>
    /// Raqamli ruxsat kodlari uchun konstruktor.
    /// </summary>
    /// <param name="permissionCodes">Ruxsat kodlari massivi.</param>
    public PermissionAuthorizeAttribute(params int[] permissionCodes)
        : base(typeof(PermissionRequirementFilter))
    {
        Arguments = [ permissionCodes ];
    }

    /// <summary>
    /// Istalgan mikroservisning o'ziga tegishli lokal enum qiymatlari uchun universal konstruktor.
    /// Har qanday enum qiymatini uning sonli qiymatiga (int) avtomatik o'giradi.
    /// </summary>
    /// <param name="permissions">Lokal enum yoki int qiymatlar.</param>
    public PermissionAuthorizeAttribute(params object[] permissions)
        : base(typeof(PermissionRequirementFilter))
    {
        Arguments = [ ExtractCodes(permissions) ];
    }

    private static int[] ExtractCodes(object[] permissions)
    {
        if (permissions == null || permissions.Length == 0)
            return [];

        var list = new List<int>(permissions.Length);
        foreach (var p in permissions)
        {
            if (p is null)
                continue;

            if (p is Enum e)
            {
                list.Add(Convert.ToInt32(e));
            }
            else if (p is int i)
            {
                list.Add(i);
            }
            else if (p is IConvertible c)
            {
                try
                {
                    list.Add(c.ToInt32(null));
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Ruxsat qiymati '{p}' butun son (int) yoki Enum bo'lishi kerak.", nameof(permissions), ex);
                }
            }
            else
            {
                throw new ArgumentException($"Ruxsat qiymati '{p}' (turi: {p.GetType().Name}) butun son (int) yoki Enum bo'lishi kerak.", nameof(permissions));
            }
        }
        return list.ToArray();
    }
}
