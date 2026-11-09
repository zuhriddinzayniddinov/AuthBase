using Core.Web.Constants;
using DatabaseBroker.DataContext;
using Entity.Enums;
using Entity.Models.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DatabaseBroker.Seeders;

/// <summary>
/// AuthBase ma'lumotlar bazasining boshlang'ich ma'lumotlarini (standart rollar, ruxsatlar)
/// avtomatik yaratish va xotiradagi keshni to'ldirish klassi.
/// </summary>
public static class AuthDataSeeder
{
    /// <summary>
    /// IHost (WebApplication) uchun seeder chaqiruvi.
    /// </summary>
    public static Task SeedAuthDataAsync(this Microsoft.Extensions.Hosting.IHost host)
    {
        return host.Services.SeedAuthDataAsync();
    }

    /// <summary>
    /// Ma'lumotlar bazasida standart rollar va ruxsatlarni tekshiradi va yo'qlarini yaratadi.
    /// Shuningdek, xotiradagi <see cref="StaticCache.Permissions"/> ni to'ldiradi.
    /// </summary>
    public static async Task SeedAuthDataAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthDataContext>();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("AuthDataSeeder");

        logger?.LogInformation("AuthBase ma'lumotlar bazasini seeder orqali tekshirish va to'ldirish boshlandi...");

        // 1. Rollarni tekshirish (Type = 1: Default User, Type = 2: SuperAdmin)
        var defaultRole = await context.Structures.FirstOrDefaultAsync(s => s.Type == 1);
        if (defaultRole == null)
        {
            defaultRole = new Structure
            {
                Name = "User",
                Type = 1,
                CreatedBy = 0,
                UpdatedBy = 0
            };
            await context.Structures.AddAsync(defaultRole);
            await context.SaveChangesAsync();
            logger?.LogInformation("Default 'User' roli (Type = 1) yaratildi. ID: {Id}", defaultRole.Id);
        }

        var superAdminRole = await context.Structures.FirstOrDefaultAsync(s => s.Type == 2);
        if (superAdminRole == null)
        {
            superAdminRole = new Structure
            {
                Name = "SuperAdmin",
                Type = 2,
                CreatedBy = 0,
                UpdatedBy = 0
            };
            await context.Structures.AddAsync(superAdminRole);
            await context.SaveChangesAsync();
            logger?.LogInformation("SuperAdmin roli (Type = 2) yaratildi. ID: {Id}", superAdminRole.Id);
        }

        // 2. UserPermissions enum'idagi barcha ruxsatlarni bazada borligini tekshirish
        var existingPermissions = await context.Permissions.ToDictionaryAsync(p => p.Code);
        var enumValues = Enum.GetValues<UserPermissions>();
        var newPermissions = new List<Permission>();

        foreach (var val in enumValues)
        {
            var code = (int)val;
            var name = val.ToString();
            var servicePrefix = code / 1000000;
            var serviceName = servicePrefix switch
            {
                10 => "AuthApi",
                20 => "CloudApi",
                30 => "ReferenceBookApi",
                40 => "MosqueApi",
                60 => "QuranCourse",
                _ => "Other"
            };

            if (!existingPermissions.TryGetValue(code, out var existing))
            {
                var perm = new Permission
                {
                    Code = code,
                    Name = name,
                    ServiceName = serviceName,
                    ModuleName = name
                };
                newPermissions.Add(perm);
            }
        }

        if (newPermissions.Count > 0)
        {
            await context.Permissions.AddRangeAsync(newPermissions);
            await context.SaveChangesAsync();
            logger?.LogInformation("{Count} ta yangi boshlang'ich ruxsatlar bazaga qo'shildi.", newPermissions.Count);
        }

        // 3. SuperAdmin ga barcha ruxsatlarni biriktirish
        var allDbPermissions = await context.Permissions.ToListAsync();
        var existingSuperAdminPermIds = await context.StructurePermissions
            .Where(sp => sp.StructureId == superAdminRole.Id)
            .Select(sp => sp.PermissionId)
            .ToHashSetAsync();

        var missingForSuperAdmin = allDbPermissions
            .Where(p => !existingSuperAdminPermIds.Contains(p.Id))
            .Select(p => new StructurePermission
            {
                StructureId = superAdminRole.Id,
                PermissionId = p.Id,
                CreatedBy = 0,
                UpdatedBy = 0
            })
            .ToList();

        if (missingForSuperAdmin.Count > 0)
        {
            await context.StructurePermissions.AddRangeAsync(missingForSuperAdmin);
            await context.SaveChangesAsync();
            logger?.LogInformation("SuperAdmin roliga {Count} ta yangi ruxsat biriktirildi.", missingForSuperAdmin.Count);
        }

        // 4. Default User roliga standart ruxsatlarni biriktirish (LogOut, ViewMyPermissions, ViewProfile)
        var defaultUserCodes = new HashSet<int>
        {
            (int)UserPermissions.LogOut,
            (int)UserPermissions.ViewMyPermissions,
            (int)UserPermissions.ViewProfile
        };

        var existingDefaultPermIds = await context.StructurePermissions
            .Where(sp => sp.StructureId == defaultRole.Id)
            .Select(sp => sp.PermissionId)
            .ToHashSetAsync();

        var missingForDefault = allDbPermissions
            .Where(p => defaultUserCodes.Contains(p.Code) && !existingDefaultPermIds.Contains(p.Id))
            .Select(p => new StructurePermission
            {
                StructureId = defaultRole.Id,
                PermissionId = p.Id,
                CreatedBy = 0,
                UpdatedBy = 0
            })
            .ToList();

        if (missingForDefault.Count > 0)
        {
            await context.StructurePermissions.AddRangeAsync(missingForDefault);
            await context.SaveChangesAsync();
            logger?.LogInformation("Default User roliga {Count} ta asosiy ruxsat biriktirildi.", missingForDefault.Count);
        }

        // 5. Xotiradagi StaticCache.Permissions ni barcha strukturalar bilan to'ldirish
        await ReloadPermissionsCacheAsync(context, logger);
    }

    /// <summary>
    /// Xotiradagi <see cref="CoreStaticCache.Permissions"/> keshini ma'lumotlar bazasidan qayta yuklaydi.
    /// </summary>
    public static async Task ReloadPermissionsCacheAsync(AuthDataContext context, ILogger? logger = null)
    {
        var structurePermissions = await context.StructurePermissions
            .AsNoTracking()
            .Include(sp => sp.Permission)
            .Where(sp => !sp.IsDelete && sp.Permission != null && !sp.Permission.IsDelete)
            .GroupBy(sp => sp.StructureId)
            .Select(g => new
            {
                StructureId = g.Key,
                Codes = g.Select(x => x.Permission.Code).Distinct().ToList()
            })
            .ToListAsync();

        StaticCache.Permissions.Clear();
        foreach (var item in structurePermissions)
        {
            StaticCache.Permissions[item.StructureId] = item.Codes;
        }

        logger?.LogInformation(
            "StaticCache.Permissions muvaffaqiyatli yangilandi. Jami {Count} ta struktura keshlandi.",
            structurePermissions.Count);
    }
}
