using Core.Domain.Permissions;
using Core.Infrastructure.Permissions;
using DatabaseBroker.DataContext;
using Entity.Models.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuthService.Services;

/// <summary>
/// AuthApi ma'lumotlar bazasida ruxsatlarni saqlash va boshqa mikroservislarga
/// faqat o'zlariga tegishli struktura va ruxsat kodlarini berish uchun xizmat.
/// </summary>
public class AuthPermissionStorageHandler : IPermissionStorageHandler
{
    private readonly AuthDataContext _dbContext;
    private readonly ILogger<AuthPermissionStorageHandler> _logger;

    public AuthPermissionStorageHandler(
        AuthDataContext dbContext,
        ILogger<AuthPermissionStorageHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> HandlePermissionsReceivedAsync(
        string serviceName,
        int? serviceCode,
        IReadOnlyList<PermissionDefinition> permissions,
        CancellationToken cancellationToken = default)
    {
        if (permissions == null || permissions.Count == 0)
            return 0;

        var codes = permissions.Select(p => p.Code).ToList();
        var existingPermissions = await _dbContext.Permissions
            .Where(p => codes.Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, cancellationToken);

        int processed = 0;
        var newPermissions = new List<Permission>();

        foreach (var def in permissions)
        {
            if (existingPermissions.TryGetValue(def.Code, out var existing))
            {
                existing.Name = def.Name;
                existing.Description = def.Description;
                existing.ModuleName = def.ModuleName;
                existing.ServiceName = def.ServiceName ?? serviceName;
                processed++;
            }
            else
            {
                var newEntity = new Permission
                {
                    Code = def.Code,
                    Name = def.Name,
                    Description = def.Description,
                    ModuleName = def.ModuleName,
                    ServiceName = def.ServiceName ?? serviceName
                };
                newPermissions.Add(newEntity);
                processed++;
            }
        }

        if (newPermissions.Count > 0)
        {
            await _dbContext.Permissions.AddRangeAsync(newPermissions, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // SuperAdmin (Type == 2) ga yangi qo'shilgan barcha ruxsatlarni avtomatik biriktiramiz
        var superAdmin = await _dbContext.Structures
            .FirstOrDefaultAsync(s => s.Type == 2, cancellationToken);

        if (superAdmin != null && newPermissions.Count > 0)
        {
            var superAdminStructurePermissions = newPermissions.Select(p => new StructurePermission
            {
                StructureId = superAdmin.Id,
                PermissionId = p.Id,
                CreatedBy = 0,
                UpdatedBy = 0
            });
            await _dbContext.StructurePermissions.AddRangeAsync(superAdminStructurePermissions, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Shuningdek xotiradagi keshni yangilaymiz
            if (CoreStaticCache.Permissions.TryGetValue(superAdmin.Id, out var existingCodes))
            {
                existingCodes.AddRange(newPermissions.Select(p => p.Code));
            }
            else
            {
                CoreStaticCache.Permissions[superAdmin.Id] = newPermissions.Select(p => p.Code).ToList();
            }
        }

        return processed;
    }

    /// <inheritdoc />
    public async Task<Dictionary<long, List<int>>> GetStructurePermissionsForServiceAsync(
        string? serviceName,
        int? serviceCode,
        IReadOnlyList<int>? permissionCodes,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Permission> query = _dbContext.Permissions.AsNoTracking();

        if (permissionCodes != null && permissionCodes.Count > 0)
        {
            query = query.Where(p => permissionCodes.Contains(p.Code));
        }
        else if (serviceCode.HasValue)
        {
            var minCode = serviceCode.Value * 1000000;
            var maxCode = (serviceCode.Value + 1) * 1000000;
            query = query.Where(p => (p.Code >= minCode && p.Code < maxCode) || p.ServiceName == serviceName);
        }
        else if (!string.IsNullOrEmpty(serviceName))
        {
            query = query.Where(p => p.ServiceName == serviceName);
        }

        var matchedPermissions = await query.Select(p => new { p.Id, p.Code }).ToListAsync(cancellationToken);
        if (matchedPermissions.Count == 0)
            return [];

        var matchedPermissionIds = matchedPermissions.Select(p => p.Id).ToList();
        var permissionIdToCode = matchedPermissions.ToDictionary(p => p.Id, p => p.Code);

        var structurePermissions = await _dbContext.StructurePermissions
            .AsNoTracking()
            .Where(sp => matchedPermissionIds.Contains(sp.PermissionId))
            .Select(sp => new { sp.StructureId, sp.PermissionId })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<long, List<int>>();
        foreach (var sp in structurePermissions)
        {
            if (!result.TryGetValue(sp.StructureId, out var codes))
            {
                codes = [];
                result[sp.StructureId] = codes;
            }

            if (permissionIdToCode.TryGetValue(sp.PermissionId, out var code) && !codes.Contains(code))
            {
                codes.Add(code);
            }
        }

        return result;
    }
}
