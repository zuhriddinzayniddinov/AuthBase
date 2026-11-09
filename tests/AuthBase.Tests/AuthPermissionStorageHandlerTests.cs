using AuthService.Services;
using Core.Domain.Permissions;
using Core.Web.Constants;
using DatabaseBroker.DataContext;
using DatabaseBroker.Seeders;
using Entity.Contracts.StructurePermissions;
using Entity.Models.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuthBase.Tests;

public class AuthPermissionStorageHandlerTests
{
    private AuthDataContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AuthDataContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AuthDataContext(options);
    }

    [Fact]
    public async Task HandlePermissionsReceivedAsync_InsertsPermissions_AndBindsToSuperAdmin()
    {
        using var context = CreateInMemoryContext(nameof(HandlePermissionsReceivedAsync_InsertsPermissions_AndBindsToSuperAdmin));

        // Create SuperAdmin structure (Type = 2)
        var superAdmin = new Structure { Name = "SuperAdmin", Type = 2 };
        await context.Structures.AddAsync(superAdmin);
        await context.SaveChangesAsync();

        var handler = new AuthPermissionStorageHandler(context, NullLogger<AuthPermissionStorageHandler>.Instance);

        var perms = new List<PermissionDefinition>
        {
            new(60100001, "CreateCourse", "Create course permission", "Courses", "QuranCourse"),
            new(60100002, "DeleteCourse", "Delete course permission", "Courses", "QuranCourse")
        };

        var count = await handler.HandlePermissionsReceivedAsync("QuranCourse", 60, perms);

        Assert.Equal(2, count);

        // Verify permissions exist in DB
        var dbPerms = await context.Permissions.ToListAsync();
        Assert.Equal(2, dbPerms.Count);

        // Verify SuperAdmin received these permissions
        var superAdminPerms = await context.StructurePermissions
            .Where(sp => sp.StructureId == superAdmin.Id)
            .ToListAsync();

        Assert.Equal(2, superAdminPerms.Count);
    }

    [Fact]
    public async Task StructurePermissionGrpcService_ReturnsOnlyTargetServicePermissions_ExcludesForeignData()
    {
        using var context = CreateInMemoryContext(nameof(StructurePermissionGrpcService_ReturnsOnlyTargetServicePermissions_ExcludesForeignData));

        // Create 3 structures
        var defaultUserRole = new Structure { Id = 1, Name = "User", Type = 1 };
        var superAdminRole = new Structure { Id = 2, Name = "SuperAdmin", Type = 2 };
        var hrRole = new Structure { Id = 3, Name = "HR", Type = 0 };
        await context.Structures.AddRangeAsync(defaultUserRole, superAdminRole, hrRole);

        // Create permissions:
        // QuranCourse: 60100001, 60100002
        // HR Service: 70100001
        var perm1 = new Permission { Id = 10, Code = 60100001, Name = "CreateCourse", ServiceName = "QuranCourse" };
        var perm2 = new Permission { Id = 11, Code = 60100002, Name = "DeleteCourse", ServiceName = "QuranCourse" };
        var foreignPerm = new Permission { Id = 20, Code = 70100001, Name = "ViewEmployee", ServiceName = "HRService" };
        await context.Permissions.AddRangeAsync(perm1, perm2, foreignPerm);

        // Bindings:
        // User (1): only perm1 (60100001)
        // SuperAdmin (2): perm1, perm2, foreignPerm
        // HR (3): only foreignPerm (70100001)
        await context.StructurePermissions.AddRangeAsync(
            new StructurePermission { StructureId = 1, PermissionId = 10 },
            new StructurePermission { StructureId = 2, PermissionId = 10 },
            new StructurePermission { StructureId = 2, PermissionId = 11 },
            new StructurePermission { StructureId = 2, PermissionId = 20 },
            new StructurePermission { StructureId = 3, PermissionId = 20 }
        );
        await context.SaveChangesAsync();

        var service = new StructurePermissionGrpcService(context, NullLogger<StructurePermissionGrpcService>.Instance);

        // Act: QuranCourse microservice queries its permissions via gRPC
        var response = await service.GetServiceStructurePermissionsAsync(new GetServiceStructurePermissionsRequest
        {
            ServiceName = "QuranCourse",
            ServiceCode = 60,
            PermissionCodes = [60100001, 60100002]
        });

        // Assert:
        Assert.True(response.Success);
        var itemsMap = response.Items.ToDictionary(i => i.StructureId, i => i.PermissionCodes);

        // 1. Structure 3 (HR) MUST NOT be present because it has 0 QuranCourse permissions
        Assert.False(itemsMap.ContainsKey(3));

        // 2. Structure 1 (User) has only [60100001]
        Assert.True(itemsMap.ContainsKey(1));
        Assert.Equal([60100001], itemsMap[1]);

        // 3. Structure 2 (SuperAdmin) has [60100001, 60100002] but NOT 70100001 (foreign)
        Assert.True(itemsMap.ContainsKey(2));
        Assert.Contains(60100001, itemsMap[2]);
        Assert.Contains(60100002, itemsMap[2]);
        Assert.DoesNotContain(70100001, itemsMap[2]);
        Assert.Equal(2, itemsMap[2].Count);
    }

    [Fact]
    public async Task ReloadPermissionsCacheAsync_PopulatesStaticCache()
    {
        using var context = CreateInMemoryContext(nameof(ReloadPermissionsCacheAsync_PopulatesStaticCache));

        var s1 = new Structure { Id = 100, Name = "RoleA", Type = 1 };
        var p1 = new Permission { Id = 200, Code = 10100001, Name = "P1" };
        await context.Structures.AddAsync(s1);
        await context.Permissions.AddAsync(p1);
        await context.StructurePermissions.AddAsync(new StructurePermission { StructureId = 100, PermissionId = 200 });
        await context.SaveChangesAsync();

        StaticCache.Permissions.Clear();

        await AuthDataSeeder.ReloadPermissionsCacheAsync(context);

        Assert.True(StaticCache.Permissions.ContainsKey(100));
        Assert.Equal([10100001], StaticCache.Permissions[100]);
    }
}
