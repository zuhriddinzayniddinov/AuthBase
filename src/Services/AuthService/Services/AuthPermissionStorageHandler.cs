using Core.Domain.Permissions;
using Core.Infrastructure.Permissions;
using Core.Web.Constants;
using DatabaseBroker.DataContext;
using Entity.Models.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuthService.Services;

/// <summary>
/// AuthApi ma'lumotlar bazasida boshqa mikroservislardan kelgan ruxsatlarni
/// saqlash va yangilash (Upsert) xizmati.
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
            if (StaticCache.Permissions.TryGetValue(superAdmin.Id, out var existingCodes))
            {
                existingCodes.AddRange(newPermissions.Select(p => p.Code));
            }
            else
            {
                StaticCache.Permissions[superAdmin.Id] = newPermissions.Select(p => p.Code).ToList();
            }
        }

        return processed;
    }
}
